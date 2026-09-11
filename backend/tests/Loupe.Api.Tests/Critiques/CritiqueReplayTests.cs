// Given a retained critique request key, when a request is repeated across API
// instances or uncertain commits, then it resolves once without changing inputs.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Persistence;
using Loupe.Api.Tests.Photographs;
using Xunit;

namespace Loupe.Api.Tests.Critiques;

public sealed class CritiqueReplayTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private ApiFactory CreateFactory(string? mode = "Live", TransientCommitFailure? failure = null) =>
        new(database.ConnectionString, database.MediaRoot)
        {
            Settings = new Dictionary<string, string?> { ["Ai:Mode"] = mode, ["Ai:ApiKey"] = mode is null ? null : "fixture-only-key" },
            TransactionInterceptor = failure
        };

    [Fact]
    public async Task L2_030_1_Concurrent_replays_resolve_one_operation_across_instances()
    {
        await using var first = CreateFactory();
        await using var second = CreateFactory();
        var subject = Guid.NewGuid().ToString();
        using var client = await first.CreateAuthenticatedClientAsync(subject);
        using var other = await second.CreateAuthenticatedClientAsync(subject);
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        var key = Guid.NewGuid().ToString();
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(index => SubmitAsync(index % 2 == 0 ? client : other, id, key)));
        var operationIds = new HashSet<Guid>();
        foreach (var response in responses)
        {
            using (response)
            {
                Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
                operationIds.Add((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
            }
        }
        Assert.Single(operationIds);
    }

    [Fact]
    public async Task L2_030_1_Replay_after_restart_ignores_later_configuration_and_photo_edits()
    {
        var subject = Guid.NewGuid().ToString();
        var key = Guid.NewGuid().ToString();
        Guid id;
        string original;
        await using (var first = CreateFactory())
        {
            using var client = await first.CreateAuthenticatedClientAsync(subject);
            id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
            using var accepted = await SubmitAsync(client, id, key);
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
            original = await accepted.Content.ReadAsStringAsync();
            using var edited = await client.PutAsJsonAsync($"/api/photographs/{id}/brief", new { revision = 1, intent = "Changed after admission" });
            edited.EnsureSuccessStatusCode();
        }
        await using var restarted = CreateFactory(null);
        using var later = await restarted.CreateAuthenticatedClientAsync(subject);
        using var replay = await SubmitAsync(later, id, key);
        Assert.Equal(HttpStatusCode.Accepted, replay.StatusCode);
        Assert.Equal(original, await replay.Content.ReadAsStringAsync());
        using var deleted = await later.DeleteAsync($"/api/photographs/{id}?revision=2");
        deleted.EnsureSuccessStatusCode();
        using var removedReplay = await SubmitAsync(later, id, key);
        Assert.Equal(HttpStatusCode.NotFound, removedReplay.StatusCode);
    }

    [Fact]
    public async Task L2_030_2_Changed_payload_conflicts_and_owner_keys_are_independent()
    {
        await using var factory = CreateFactory();
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var other = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(owner)).GetProperty("id").GetGuid();
        var otherId = (await PhotographFixture.UploadAsync(other)).GetProperty("id").GetGuid();
        var key = new string('k', 128);
        using var accepted = await SubmitAsync(owner, id, key);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        using var conflict = await SubmitAsync(owner, id, key, regenerate: true);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("operation_conflict", (await conflict.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        using var independent = await SubmitAsync(other, otherId, key);
        Assert.Equal(HttpStatusCode.Accepted, independent.StatusCode);
        Assert.NotEqual(accepted.Headers.Location, independent.Headers.Location);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task L2_030_4_Uncertain_commit_can_be_retried_on_another_instance(bool committed)
    {
        var failure = new TransientCommitFailure(committed);
        await using var first = CreateFactory(failure: failure);
        var subject = Guid.NewGuid().ToString();
        using var client = await first.CreateAuthenticatedClientAsync(subject);
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        var key = Guid.NewGuid().ToString();
        failure.Arm();
        using var failed = await SubmitAsync(client, id, key);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(5), failed.Headers.RetryAfter?.Delta);
        Assert.DoesNotContain("Private backend", await failed.Content.ReadAsStringAsync());
        // A committed request must resolve even if new admissions are disabled.
        await using var second = CreateFactory(committed ? null : "Live");
        using var later = await second.CreateAuthenticatedClientAsync(subject);
        using var retry = await SubmitAsync(later, id, key);
        Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode);
        using var repeat = await SubmitAsync(later, id, key);
        Assert.Equal(HttpStatusCode.Accepted, repeat.StatusCode);
        Assert.Equal(await retry.Content.ReadAsStringAsync(), await repeat.Content.ReadAsStringAsync());
        using var status = await later.GetAsync(retry.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
    }

    [Theory]
    [InlineData("contains space")]
    [InlineData("non-ascii-é")]
    [InlineData("oversized")]
    public async Task L2_029_2_Invalid_keys_are_rejected(string key)
    {
        await using var factory = CreateFactory();
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var response = await SubmitAsync(client, id, key == "oversized" ? new string('k', 129) : key);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_request", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    private static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid id, string key, bool regenerate = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{id}/critique")
        { Content = JsonContent.Create(new { revision = 1, regenerate }) };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        return await client.SendAsync(request);
    }
}
