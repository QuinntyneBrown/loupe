using System.Net.Http.Headers;
using NetVips;

namespace Loupe.Api.Tests.References;

public static class ReferenceFixture
{
    public static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, IReadOnlyDictionary<string, string>? fields = null, string? key = null)
    {
        using var image = Image.Black(8, 6, bands: 3);
        using var upload = new MultipartFormDataContent();
        var part = new ByteArrayContent(image.PngsaveBuffer());
        part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        upload.Add(part, "image", "Reference.png");
        foreach (var field in fields ?? new Dictionary<string, string>()) upload.Add(new StringContent(field.Value), field.Key);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/references/images") { Content = upload };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }
}
