// Given a transient failure before or after the real database commit, when an upload
// is retried on another API instance, then it resolves safely and saved media remains readable.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Persistence;
using NetVips;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class UploadCommitFailureTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task L2_001_4_030_4_Commit_failures_are_retryable_without_losing_or_duplicating_a_saved_photo(bool committed)
    {
        var failure = new TransientCommitFailure(committed);
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot) { TransactionInterceptor = failure };
        var subject = Guid.NewGuid().ToString();
        using var client = await factory.CreateAuthenticatedClientAsync(subject);
        using var image = Image.Black(8, 6, bands: 3);
        var bytes = image.PngsaveBuffer();
        var key = Guid.NewGuid().ToString();
        var filesBefore = Directory.Exists(database.MediaRoot) ? Directory.GetFiles(database.MediaRoot).Length : 0;
        failure.Arm();
        using var failed = await SendAsync(client, key, bytes);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(5), failed.Headers.RetryAfter?.Delta);
        var body = await failed.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Private backend", body);
        await using var second = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var later = await second.CreateAuthenticatedClientAsync(subject);
        var before = await later.GetFromJsonAsync<JsonElement>("/api/photographs");
        Assert.Equal(committed ? 1 : 0, before.GetProperty("items").GetArrayLength());
        using var retry = await SendAsync(later, key, bytes);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        var saved = await retry.Content.ReadFromJsonAsync<JsonElement>();
        if (committed)
            Assert.Equal(before.GetProperty("items")[0].GetProperty("id").GetGuid(), saved.GetProperty("id").GetGuid());
        var list = await later.GetFromJsonAsync<JsonElement>("/api/photographs");
        Assert.Single(list.GetProperty("items").EnumerateArray());
        using var preview = await later.GetAsync(saved.GetProperty("previewUrl").GetString());
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var decoded = Image.NewFromBuffer(await preview.Content.ReadAsByteArrayAsync());
        Assert.Equal(8, decoded.Width);
        Assert.Equal(6, decoded.Height);
        if (committed) Assert.Equal(filesBefore + 2, Directory.GetFiles(database.MediaRoot).Length);
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
