using System.Net.Http.Headers;
using System.Text.Json;
using NetVips;

namespace Loupe.Api.Tests.Photographs;

public static class PhotographFixture
{
    public static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, HttpContent upload, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/photographs") { Content = upload };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    public static async Task<JsonElement> UploadAsync(HttpClient client, string title = "Window light")
    {
        using var image = Image.Black(8, 6, bands: 3);
        using var upload = new MultipartFormDataContent();
        var part = new ByteArrayContent(image.PngsaveBuffer());
        part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        upload.Add(part, "image", "Study.png");
        upload.Add(new StringContent(title), "title");
        using var response = await PhotographFixture.SubmitAsync(client, upload);
        response.EnsureSuccessStatusCode();
        using var saved = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return saved.RootElement.Clone();
    }
}
