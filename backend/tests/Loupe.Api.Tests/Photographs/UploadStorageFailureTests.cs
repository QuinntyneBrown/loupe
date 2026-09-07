// Given unavailable private storage, when saving fails and storage is restored,
// then the same operation key can be retried without a partial photograph.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NetVips;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class UploadStorageFailureTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_001_4_Storage_failure_is_retryable_and_does_not_reserve_a_failed_upload_key()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync();
        using var image = Image.Black(8, 6, bands: 3);
        var bytes = image.PngsaveBuffer();
        var key = Guid.NewGuid().ToString();
        Directory.CreateDirectory(Path.GetDirectoryName(database.MediaRoot)!);
        await File.WriteAllTextAsync(database.MediaRoot, "Controlled obstacle at the test-owned media directory.");
        try
        {
            using var failed = await SendAsync(client, key, bytes);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
            Assert.Equal(TimeSpan.FromSeconds(5), failed.Headers.RetryAfter?.Delta);
            var list = await client.GetFromJsonAsync<JsonElement>("/api/photographs");
            Assert.Empty(list.GetProperty("items").EnumerateArray());
        }
        finally { File.Delete(database.MediaRoot); }
        using var retry = await SendAsync(client, key, bytes);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        var saved = await retry.Content.ReadFromJsonAsync<JsonElement>();
        using var preview = await client.GetAsync(saved.GetProperty("previewUrl").GetString());
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var after = await client.GetFromJsonAsync<JsonElement>("/api/photographs");
        Assert.Single(after.GetProperty("items").EnumerateArray());
        Assert.Equal(2, Directory.GetFiles(database.MediaRoot).Length);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string key, byte[] bytes)
    {
        using var upload = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        upload.Add(part, "image", "Study.png");
        return await PhotographFixture.SubmitAsync(client, upload, key);
    }
}
