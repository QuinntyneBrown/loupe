// Given an uncertain deletion commit, when the owner retries on another API
// instance, then deletion and its durable journal resolve together without resurrection.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Persistence;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class DeleteCommitFailureTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task L2_031_5_6_032_5_Commit_failure_keeps_content_and_journal_atomic_and_retryable(bool committed)
    {
        var failure = new TransientCommitFailure(committed);
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot) { TransactionInterceptor = failure };
        var subject = Guid.NewGuid().ToString();
        using var client = await factory.CreateAuthenticatedClientAsync(subject);
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        failure.Arm();
        using var failed = await client.DeleteAsync($"/api/photographs/{id}?revision=1");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(5), failed.Headers.RetryAfter?.Delta);
        Assert.DoesNotContain("Private backend", await failed.Content.ReadAsStringAsync());
        await using var second = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var later = await second.CreateAuthenticatedClientAsync(subject);
        using var read = await later.GetAsync($"/api/photographs/{id}");
        Assert.Equal(committed ? HttpStatusCode.NotFound : HttpStatusCode.OK, read.StatusCode);
        using var retry = await later.DeleteAsync($"/api/photographs/{id}?revision=1");
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var operation = await retry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", operation.GetProperty("status").GetString());
        using var status = await later.GetAsync($"/api/deletions/{operation.GetProperty("id").GetGuid()}");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.Equal(operation.GetRawText(), (await status.Content.ReadFromJsonAsync<JsonElement>()).GetRawText());
        using var repeated = await later.DeleteAsync($"/api/photographs/{id}?revision=1");
        Assert.Equal(operation.GetRawText(), (await repeated.Content.ReadFromJsonAsync<JsonElement>()).GetRawText());
        using var unavailable = await later.GetAsync($"/api/photographs/{id}/preview");
        Assert.Equal(HttpStatusCode.NotFound, unavailable.StatusCode);
        var list = await later.GetFromJsonAsync<JsonElement>("/api/photographs");
        Assert.Empty(list.GetProperty("items").EnumerateArray());
    }
}
