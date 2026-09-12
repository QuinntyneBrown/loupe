using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.Photographers;

public sealed class PhotographerSummaryAdmissionTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private ApiFactory Factory(bool configured = true) => new(database.ConnectionString, database.MediaRoot)
    { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = configured ? "fixture-only" : null } };

    [Fact]
    public async Task Summary_admission_is_durable_private_and_deduplicated_without_changing_manual_fields()
    {
        var subject = Guid.NewGuid().ToString(); Guid id; string operation;
        await using (var factory = Factory())
        {
            using var owner = await factory.CreateAuthenticatedClientAsync(subject);
            id = await Save(owner); var before = await owner.GetStringAsync($"/api/photographers/{id}");
            using var admitted = await Submit(owner, id, 1, "summary"); Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
            operation = await admitted.Content.ReadAsStringAsync(); var value = JsonSerializer.Deserialize<JsonElement>(operation);
            Assert.Equal("PhotographerSummary", value.GetProperty("type").GetString()); Assert.Equal("Queued", value.GetProperty("status").GetString());
            Assert.Equal(id, value.GetProperty("resourceId").GetGuid());
            using var replay = await Submit(owner, id, 1, "summary"); Assert.Equal(operation, await replay.Content.ReadAsStringAsync());
            using var duplicate = await Submit(owner, id, 1); Assert.Equal(operation, await duplicate.Content.ReadAsStringAsync());
            Assert.Equal(before, await owner.GetStringAsync($"/api/photographers/{id}"));
            using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
            using var hidden = await stranger.GetAsync($"/api/photographers/{id}/summary-analysis"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
            using var foreign = await Submit(stranger, id, 1); Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
            using var stale = await Submit(owner, id, 2); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        }
        await using var restart = Factory(); using var later = await restart.CreateAuthenticatedClientAsync(subject);
        Assert.Equal(operation, await later.GetStringAsync($"/api/photographers/{id}/summary-analysis"));
    }

    [Fact]
    public async Task Unconfigured_summary_is_explicitly_unavailable_and_manual_bookmark_is_retained()
    {
        await using var factory = Factory(false); using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = await Save(owner);
        using var current = await owner.GetAsync($"/api/photographers/{id}/summary-analysis"); Assert.Equal(HttpStatusCode.NoContent, current.StatusCode);
        using var rejected = await Submit(owner, id, 1); Assert.Equal(HttpStatusCode.ServiceUnavailable, rejected.StatusCode);
        Assert.Equal("Manual summary", (await owner.GetFromJsonAsync<JsonElement>($"/api/photographers/{id}")).GetProperty("summary").GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Saving_queues_a_summary_and_url_changes_or_deletion_cancel_old_work(bool delete)
    {
        await using var factory = Factory(); using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = await Save(owner);
        using var queued = await owner.GetAsync($"/api/photographers/{id}/summary-analysis"); Assert.Equal(HttpStatusCode.OK, queued.StatusCode);
        var old = (await queued.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var mutation = delete ? await owner.DeleteAsync($"/api/photographers/{id}?revision=1")
            : await owner.PutAsJsonAsync($"/api/photographers/{id}", new { revision = 1, name = "Casey", portfolioUrl = "https://new.example/", summary = "Manual summary", notes = "Private notes" });
        mutation.EnsureSuccessStatusCode();
        Assert.Equal("Canceled", (await owner.GetFromJsonAsync<JsonElement>($"/api/operations/{old}")).GetProperty("status").GetString());
        if (!delete)
        {
            var current = await owner.GetFromJsonAsync<JsonElement>($"/api/photographers/{id}/summary-analysis");
            Assert.NotEqual(old, current.GetProperty("id").GetGuid()); Assert.Equal("Queued", current.GetProperty("status").GetString());
            var bookmark = await owner.GetFromJsonAsync<JsonElement>($"/api/photographers/{id}"); Assert.Equal("Manual summary", bookmark.GetProperty("summary").GetString());
        }
    }

    [Fact]
    public async Task A_full_queue_keeps_the_new_bookmark_with_an_explicit_retryable_summary_status()
    {
        await using var factory = Factory(); using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        for (var index = 0; index < 5; index++) await Save(owner, $"https://portfolio{index}.example/");
        var id = await Save(owner, "https://last.example/");
        var operation = await owner.GetFromJsonAsync<JsonElement>($"/api/photographers/{id}/summary-analysis");
        Assert.Equal("Failed", operation.GetProperty("status").GetString()); Assert.Equal("analysis_limit", operation.GetProperty("failureCode").GetString());
        Assert.Equal(6, (await owner.GetFromJsonAsync<JsonElement>("/api/photographers")).GetProperty("totalCount").GetInt32());
        Assert.Equal("Manual summary", (await owner.GetFromJsonAsync<JsonElement>($"/api/photographers/{id}")).GetProperty("summary").GetString());
    }

    private static async Task<Guid> Save(HttpClient owner, string portfolioUrl = "https://casey.example/")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/photographers") { Content = JsonContent.Create(new { name = "Casey", portfolioUrl, summary = "Manual summary", notes = "Private notes" }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString()); using var response = await owner.SendAsync(request); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("photographer").GetProperty("id").GetGuid();
    }
    private static async Task<HttpResponseMessage> Submit(HttpClient owner, Guid id, long revision, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographers/{id}/summary-analysis") { Content = JsonContent.Create(new { revision }) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString()); return await owner.SendAsync(request);
    }
}
