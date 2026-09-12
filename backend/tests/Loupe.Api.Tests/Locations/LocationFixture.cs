using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NetVips;
using Xunit;

namespace Loupe.Api.Tests.Locations;

public static class LocationFixture
{
    public static async Task<JsonElement> CreateAsync(HttpClient client, object? input = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/locations") { Content = JsonContent.Create(input ?? new { name = "Kew Bridge foreshore" }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static Task<HttpResponseMessage> SubmitImageAsync(HttpClient client, Guid locationId, string? key = null, string format = "png", int width = 8, int height = 6)
    {
        using var image = Image.Black(width, height, bands: 3);
        var bytes = format switch
        {
            "jpeg" => image.JpegsaveBuffer(),
            "webp" => image.WebpsaveBuffer(),
            "heic" => image.HeifsaveBuffer(compression: Enums.ForeignHeifCompression.Hevc),
            _ => image.PngsaveBuffer()
        };
        return SubmitImageAsync(client, locationId, bytes, "image/" + format, key);
    }

    public static async Task<HttpResponseMessage> SubmitImageAsync(HttpClient client, Guid locationId, byte[] bytes, string contentType, string? key = null)
    {
        using var upload = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        upload.Add(part, "image", "Location." + contentType[6..]);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/locations/{locationId}/images") { Content = upload };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    public static async Task<JsonElement> AddImageAsync(HttpClient client, Guid locationId, string format = "png")
    {
        using var response = await SubmitImageAsync(client, locationId, format: format);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static Guid[] ImageIds(JsonElement location) => location.GetProperty("images").EnumerateArray().Select(image => image.GetProperty("id").GetGuid()).ToArray();
}
