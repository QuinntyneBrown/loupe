using System.Net.Http.Headers;
using System.Text.Json;
using NetVips;

namespace Loupe.Api.Tests.Photographs;

public static class PhotographFixture
{
    public static async Task<JsonElement> UploadAsync(HttpClient client, string title = "Window light")
    {
        using var image = Image.Black(8, 6, bands: 3);
        using var upload = new MultipartFormDataContent();
        var part = new ByteArrayContent(image.PngsaveBuffer());
        part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        upload.Add(part, "image", "Study.png");
        upload.Add(new StringContent(title), "title");
        using var response = await client.PostAsync("/api/photographs", upload);
        response.EnsureSuccessStatusCode();
        using var saved = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return saved.RootElement.Clone();
    }
}
