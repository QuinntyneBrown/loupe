// Given a saved source, when import is explicitly admitted, then the job is durable,
// owner scoped and independently keyed; capacity failure preserves the bookmark.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Photographs;
using Xunit;

namespace Loupe.Api.Tests.References;

public sealed class ReferenceImportAdmissionTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private ApiFactory Factory(string? mode = "Demo") => new(database.ConnectionString, database.MediaRoot)
    { Settings = new Dictionary<string, string?> { ["Imports:Mode"] = mode, ["Ai:Mode"] = "Demo" } };

    [Fact]
    public async Task L2_010_033_Admission_survives_restart_and_status_does_not_disclose_source_or_notes()
    {
        var subject = Guid.NewGuid().ToString();
        string location, original;
        await using (var factory = Factory())
        {
            using var client = await factory.CreateAuthenticatedClientAsync(subject);
            var reference = await SaveAsync(client);
            using var response = await SubmitAsync(client, reference.GetProperty("id").GetGuid());
            Assert.True(response.StatusCode == HttpStatusCode.Accepted, factory.Failure.Exception?.ToString() ?? response.StatusCode.ToString());
            location = response.Headers.Location!.OriginalString;
            original = await response.Content.ReadAsStringAsync();
            var operation = JsonSerializer.Deserialize<JsonElement>(original);
            Assert.Equal("ReferenceImport", operation.GetProperty("type").GetString());
            Assert.Equal("Queued", operation.GetProperty("status").GetString());
            Assert.Equal("Demo", operation.GetProperty("mode").GetString());
            Assert.Equal(reference.GetProperty("id").GetGuid(), operation.GetProperty("resourceId").GetGuid());
            Assert.Equal($"/api/operations/{operation.GetProperty("id").GetGuid()}", location);
            Assert.DoesNotContain("source.example", original);
            Assert.DoesNotContain("Private context", original);
            var stored = await client.GetFromJsonAsync<JsonElement>($"/api/references/{reference.GetProperty("id").GetGuid()}");
            Assert.Equal(reference.ToString(), stored.ToString());
        }
        await using var restart = Factory();
        using var later = await restart.CreateAuthenticatedClientAsync(subject);
        Assert.Equal(original, await later.GetStringAsync(location));
    }

    [Fact]
    public async Task L2_030_035_Concurrent_keys_and_equivalent_active_requests_share_one_operation()
    {
        await using var first = Factory(); await using var second = Factory();
        var subject = Guid.NewGuid().ToString();
        using var client = await first.CreateAuthenticatedClientAsync(subject);
        using var other = await second.CreateAuthenticatedClientAsync(subject);
        var id = (await SaveAsync(client)).GetProperty("id").GetGuid();
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(index => SubmitAsync(index % 2 == 0 ? client : other, id, key: index < 3 ? "same-key" : Guid.NewGuid().ToString())));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.Accepted, response.StatusCode));
            Assert.Single(responses.Select(response => response.Headers.Location).Distinct());
            using var edit = await client.PutAsJsonAsync($"/api/references/{id}", new { revision = 1, title = "Edited title", sourceUrl = "https://source.example/photo", notes = "Changed notes" });
            edit.EnsureSuccessStatusCode();
            using var equivalent = await SubmitAsync(client, id, 2);
            Assert.Equal(HttpStatusCode.Accepted, equivalent.StatusCode);
            Assert.Equal(responses[0].Headers.Location, equivalent.Headers.Location);
            using var conflict = await SubmitAsync(client, id, 2, "same-key");
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
            Assert.Equal("operation_conflict", (await conflict.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task L2_010_4_035_Normalized_equivalent_source_edits_reuse_the_active_import()
    {
        await using var factory = Factory(); using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await SaveAsync(client, "HTTPS://Source.Example:00443/photo?keep=1#one")).GetProperty("id").GetGuid();
        using var first = await SubmitAsync(client, id); Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        using var edit = await client.PutAsJsonAsync($"/api/references/{id}", new { revision = 1, title = "Same source", sourceUrl = "https://source.example/photo?keep=1#two" }); edit.EnsureSuccessStatusCode();
        using var equivalent = await SubmitAsync(client, id, 2); Assert.Equal(HttpStatusCode.Accepted, equivalent.StatusCode);
        Assert.Equal(first.Headers.Location, equivalent.Headers.Location);
    }

    [Fact]
    public async Task L2_030_035_Changed_source_requires_active_work_to_finish_and_stale_revision_is_rejected()
    {
        await using var factory = Factory(); using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await SaveAsync(client)).GetProperty("id").GetGuid();
        using var admitted = await SubmitAsync(client, id); Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        using var edit = await client.PutAsJsonAsync($"/api/references/{id}", new { revision = 1, title = "New source", sourceUrl = "https://source.example/new" }); edit.EnsureSuccessStatusCode();
        using var stale = await SubmitAsync(client, id); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("revision_conflict", (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        using var competing = await SubmitAsync(client, id, 2); Assert.Equal(HttpStatusCode.Conflict, competing.StatusCode);
        Assert.Equal("analysis_active", (await competing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task L2_033_035_Import_and_critique_share_five_job_capacity_without_losing_sources()
    {
        await using var first = Factory(); await using var second = Factory(); var subject = Guid.NewGuid().ToString();
        using var client = await first.CreateAuthenticatedClientAsync(subject); using var other = await second.CreateAuthenticatedClientAsync(subject);
        var photo = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var critiqueRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{photo}/critique") { Content = JsonContent.Create(new { revision = 1 }) };
        critiqueRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString()); using var critique = await client.SendAsync(critiqueRequest); Assert.Equal(HttpStatusCode.Accepted, critique.StatusCode);
        var ids = new List<Guid>(); for (var i = 0; i < 6; i++) ids.Add((await SaveAsync(client, $"https://source.example/{i}")).GetProperty("id").GetGuid());
        var responses = await Task.WhenAll(ids.Select((id, index) => SubmitAsync(index % 2 == 0 ? client : other, id)));
        try
        {
            Assert.Equal(4, responses.Count(response => response.StatusCode == HttpStatusCode.Accepted));
            Assert.Equal(2, responses.Count(response => response.StatusCode == HttpStatusCode.TooManyRequests));
            foreach (var index in Enumerable.Range(0, ids.Count).Where(index => responses[index].StatusCode == HttpStatusCode.TooManyRequests))
            { Assert.Equal(TimeSpan.FromSeconds(30), responses[index].Headers.RetryAfter?.Delta); using var saved = await client.GetAsync($"/api/references/{ids[index]}"); Assert.Equal(HttpStatusCode.OK, saved.StatusCode); }
            var accepted = Array.FindIndex(responses, response => response.StatusCode == HttpStatusCode.Accepted);
            using var reuse = await SubmitAsync(client, ids[accepted]); Assert.Equal(HttpStatusCode.Accepted, reuse.StatusCode); Assert.Equal(responses[accepted].Headers.Location, reuse.Headers.Location);
            using var stranger = await first.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
            using var independent = await SubmitAsync(stranger, (await SaveAsync(stranger)).GetProperty("id").GetGuid()); Assert.Equal(HttpStatusCode.Accepted, independent.StatusCode);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task L2_033_5_Foreign_missing_and_anonymous_admission_and_status_are_private()
    {
        await using var factory = Factory(); using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await SaveAsync(client)).GetProperty("id").GetGuid();
        foreach (var target in new[] { id, Guid.NewGuid() })
        { using var hidden = await SubmitAsync(stranger, target); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode); }
        using var admitted = await SubmitAsync(client, id); Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        using var hiddenStatus = await stranger.GetAsync(admitted.Headers.Location); Assert.Equal(HttpStatusCode.NotFound, hiddenStatus.StatusCode);
        using var anonymous = factory.CreateClient(); using var denied = await SubmitAsync(anonymous, id); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [Fact]
    public async Task L2_029_030_Invalid_key_revision_and_absent_source_do_not_admit_work()
    {
        await using var factory = Factory(); using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await SaveAsync(client)).GetProperty("id").GetGuid();
        using var missing = await client.PostAsJsonAsync($"/api/references/{id}/imports", new { revision = 1 }); Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using var invalid = await SubmitAsync(client, id, 0); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var cleared = await client.PutAsJsonAsync($"/api/references/{id}", new { revision = 1, title = "No source" }); cleared.EnsureSuccessStatusCode();
        using var absent = await SubmitAsync(client, id, 2); Assert.Equal(HttpStatusCode.BadRequest, absent.StatusCode);
        Assert.True((await absent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("sourceUrl", out _));
    }

    [Theory]
    [InlineData(null, HttpStatusCode.ServiceUnavailable)]
    [InlineData("Live", HttpStatusCode.Accepted)]
    public async Task L2_036_Import_configuration_is_explicit_and_live_fetching_does_not_require_an_AI_key(string? mode, HttpStatusCode expected)
    {
        await using var factory = Factory(mode); using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await SaveAsync(client)).GetProperty("id").GetGuid();
        using var response = await SubmitAsync(client, id); Assert.Equal(expected, response.StatusCode);
        using var saved = await client.GetAsync($"/api/references/{id}"); Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
    }

    private static async Task<JsonElement> SaveAsync(HttpClient client, string sourceUrl = "https://source.example/photo")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/references/links") { Content = JsonContent.Create(new { sourceUrl, notes = "Private context" }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString()); using var response = await client.SendAsync(request); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reference").Clone();
    }
    private static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid id, long revision = 1, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/references/{id}/imports") { Content = JsonContent.Create(new { revision }) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString()); return await client.SendAsync(request);
    }
}
