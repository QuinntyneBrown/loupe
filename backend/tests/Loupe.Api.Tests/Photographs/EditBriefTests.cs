// Given a saved photograph, when its brief changes, then it persists, clears, and rejects stale/invalid/foreign writes.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class EditBriefTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_002_1_4_Brief_can_be_edited_revisited_and_cleared()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync();
        var photo = await PhotographFixture.UploadAsync(client);
        var url = $"/api/photographs/{photo.GetProperty("id").GetGuid()}";
        using var changed = await client.PutAsJsonAsync(url + "/brief", new
        {
            revision = 1,
            intent = "  Quiet\r\nmorning  ",
            genre = "  Landscape ",
            experience = "Advanced",
            requestedFeedback = "  Framing  "
        });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        await using var second = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var later = await second.CreateAuthenticatedClientAsync();
        var saved = await later.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal(2, saved.GetProperty("revision").GetInt64());
        var brief = saved.GetProperty("brief");
        Assert.Equal("Quiet\nmorning", brief.GetProperty("intent").GetString());
        Assert.Equal("Landscape", brief.GetProperty("genre").GetString());
        Assert.Equal("Advanced", brief.GetProperty("experience").GetString());
        Assert.Equal("Framing", brief.GetProperty("requestedFeedback").GetString());
        using var cleared = await later.PutAsJsonAsync(url + "/brief", new { revision = 2, intent = "  ", genre = "", experience = "", requestedFeedback = "\r\n" });
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var empty = await client.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal(3, empty.GetProperty("revision").GetInt64());
        Assert.All(empty.GetProperty("brief").EnumerateObject(), field => Assert.Equal(JsonValueKind.Null, field.Value.ValueKind));
    }

    [Theory]
    [InlineData("intent", 2001)]
    [InlineData("genre", 101)]
    [InlineData("requestedFeedback", 2001)]
    [InlineData("experience", 0)]
    [InlineData("revision", -1)]
    public async Task L2_002_2_Invalid_edit_preserves_the_saved_brief(string field, int length)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync();
        var photo = await PhotographFixture.UploadAsync(client);
        var url = $"/api/photographs/{photo.GetProperty("id").GetGuid()}";
        using var baseline = await client.PutAsJsonAsync(url + "/brief", new { revision = 1, intent = "Keep this" });
        Assert.Equal(HttpStatusCode.OK, baseline.StatusCode);
        var input = new Dictionary<string, object> { ["revision"] = 2, [field] = length < 0 ? 0 : length == 0 ? "Expert" : new string('a', length) };
        using var rejected = await client.PutAsJsonAsync(url + "/brief", input);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var problem = await rejected.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
        var saved = await client.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal("Keep this", saved.GetProperty("brief").GetProperty("intent").GetString());
        Assert.Equal(2, saved.GetProperty("revision").GetInt64());
    }

    [Fact]
    public async Task L2_030_3_Concurrent_editors_cannot_overwrite_each_other()
    {
        await using var first = new ApiFactory(database.ConnectionString, database.MediaRoot);
        await using var second = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var a = await first.CreateAuthenticatedClientAsync();
        using var b = await second.CreateAuthenticatedClientAsync();
        var photo = await PhotographFixture.UploadAsync(a);
        var url = $"/api/photographs/{photo.GetProperty("id").GetGuid()}";
        var responses = await Task.WhenAll(a.PutAsJsonAsync(url + "/brief", new { revision = 1, intent = "First" }),
            b.PutAsJsonAsync(url + "/brief", new { revision = 1, intent = "Second" }));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            var winner = await responses.Single(response => response.IsSuccessStatusCode).Content.ReadFromJsonAsync<JsonElement>();
            var saved = await a.GetFromJsonAsync<JsonElement>(url);
            Assert.Equal(winner.GetProperty("brief").GetProperty("intent").GetString(), saved.GetProperty("brief").GetProperty("intent").GetString());
            Assert.Equal(2, saved.GetProperty("revision").GetInt64());
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task L2_029_2_Foreign_brief_update_is_indistinguishable_from_absent()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync();
        using var stranger = await factory.CreateAuthenticatedClientAsync("owner-b");
        var photo = await PhotographFixture.UploadAsync(owner);
        foreach (var id in new[] { photo.GetProperty("id").GetGuid(), Guid.NewGuid() })
        {
            using var response = await stranger.PutAsJsonAsync($"/api/photographs/{id}/brief", new { revision = 1, intent = "Overwrite" });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("item_unavailable", problem.GetProperty("code").GetString());
        }
    }
}
