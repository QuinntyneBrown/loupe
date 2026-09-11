// Given equivalent or competing analysis requests, when admitted concurrently,
// then active work is reused and the per-owner cap cannot be exceeded.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Photographs;
using Xunit;

namespace Loupe.Api.Tests.Critiques;

public sealed class CritiqueDeduplicationTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private ApiFactory CreateFactory() => new(database.ConnectionString, database.MediaRoot)
    { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only-key" } };

    [Fact]
    public async Task L2_035_1_Equivalent_active_requests_reuse_one_job_including_regenerate_and_notes_edits()
    {
        await using var first = CreateFactory();
        await using var second = CreateFactory();
        var subject = Guid.NewGuid().ToString();
        using var client = await first.CreateAuthenticatedClientAsync(subject);
        using var other = await second.CreateAuthenticatedClientAsync(subject);
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(index => SubmitAsync(index % 2 == 0 ? client : other, id, regenerate: index % 2 == 0)));
        var locations = new HashSet<Uri?>();
        foreach (var response in responses)
        {
            using (response)
            {
                Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
                locations.Add(response.Headers.Location);
            }
        }
        Assert.Single(locations);
        using var notes = await client.PutAsJsonAsync($"/api/photographs/{id}/notes", new { revision = 1, notes = "Private text is not an analysis input" });
        notes.EnsureSuccessStatusCode();
        using var afterNotes = await SubmitAsync(client, id, revision: 2);
        Assert.Equal(HttpStatusCode.Accepted, afterNotes.StatusCode);
        Assert.Equal(locations.Single(), afterNotes.Headers.Location);
    }

    [Fact]
    public async Task L2_035_2_Changed_brief_conflicts_until_active_work_finishes()
    {
        await using var factory = CreateFactory();
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var first = await SubmitAsync(client, id);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        using var edit = await client.PutAsJsonAsync($"/api/photographs/{id}/brief", new { revision = 1, intent = "New intent" });
        edit.EnsureSuccessStatusCode();
        using var competing = await SubmitAsync(client, id, revision: 2, regenerate: true);
        Assert.Equal(HttpStatusCode.Conflict, competing.StatusCode);
        var error = await competing.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("analysis_active", error.GetProperty("code").GetString());
        Assert.Contains("finish", error.GetProperty("title").GetString());
        var status = await client.GetFromJsonAsync<JsonElement>(first.Headers.Location);
        Assert.Equal("Queued", status.GetProperty("status").GetString());
    }

    [Fact]
    public async Task L2_035_3_Concurrent_admission_obeys_five_job_cap_and_reuse_does_not_consume_it()
    {
        await using var first = CreateFactory();
        await using var second = CreateFactory();
        var subject = Guid.NewGuid().ToString();
        using var client = await first.CreateAuthenticatedClientAsync(subject);
        using var other = await second.CreateAuthenticatedClientAsync(subject);
        var ids = new List<Guid>();
        for (var index = 0; index < 7; index++) ids.Add((await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid());
        var responses = await Task.WhenAll(ids.Select((id, index) => SubmitAsync(index % 2 == 0 ? client : other, id)));
        try
        {
            Assert.Equal(5, responses.Count(response => response.StatusCode == HttpStatusCode.Accepted));
            Assert.Equal(2, responses.Count(response => response.StatusCode == HttpStatusCode.TooManyRequests));
            var accepted = Array.FindIndex(responses, response => response.StatusCode == HttpStatusCode.Accepted);
            var rejected = Array.FindIndex(responses, response => response.StatusCode == HttpStatusCode.TooManyRequests);
            Assert.Equal(TimeSpan.FromSeconds(30), responses[rejected].Headers.RetryAfter?.Delta);
            Assert.Equal("analysis_limit", (await responses[rejected].Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            using var reuse = await SubmitAsync(client, ids[accepted]);
            Assert.Equal(HttpStatusCode.Accepted, reuse.StatusCode);
            Assert.Equal(responses[accepted].Headers.Location, reuse.Headers.Location);
            using var saved = await client.GetAsync($"/api/photographs/{ids[rejected]}");
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            using var separateOwner = await first.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
            var ownId = (await PhotographFixture.UploadAsync(separateOwner)).GetProperty("id").GetGuid();
            using var independent = await SubmitAsync(separateOwner, ownId);
            Assert.Equal(HttpStatusCode.Accepted, independent.StatusCode);
            using var deleted = await client.DeleteAsync($"/api/photographs/{ids[accepted]}?revision=1");
            deleted.EnsureSuccessStatusCode();
            using var retry = await SubmitAsync(client, ids[rejected]);
            Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    private static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid id, long revision = 1, bool regenerate = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{id}/critique")
        { Content = JsonContent.Create(new { revision, regenerate }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }
}
