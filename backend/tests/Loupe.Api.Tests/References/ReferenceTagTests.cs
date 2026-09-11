using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.References;

// Given owned references, when active tags change, then changes are private,
// durable, validated and revision checked (L2-016, L2-029, L2-037).
public sealed class ReferenceTagTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Manual_tags_normalize_deduplicate_and_persist_without_changing_notes()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var uploaded = await ReferenceFixture.SubmitAsync(owner);
        var original = await uploaded.Content.ReadFromJsonAsync<JsonElement>();
        var id = original.GetProperty("id").GetGuid();
        using var saved = await owner.PutAsJsonAsync($"/api/references/{id}/tags", new { revision = 1, tags = new[] {
            new { name = " Cafe\u0301 ", category = "subject" }, new { name = "CAFÉ", category = "subject" },
            new { name = "soft light", category = "lighting" } } });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var result = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{id}");
        Assert.Equal(2, result.GetProperty("revision").GetInt64());
        var tags = result.GetProperty("tags").EnumerateArray().ToArray();
        Assert.Equal(2, tags.Length);
        Assert.Contains(tags, tag => tag.GetProperty("name").GetString() == "Café");
        Assert.All(tags, tag => Assert.Equal("manual", tag.GetProperty("provenance").GetString()));
        Assert.Equal(original.GetProperty("notes").GetString(), result.GetProperty("notes").GetString());
        using var removed = await owner.PutAsJsonAsync($"/api/references/{id}/tags", new { revision = 2, tags = Array.Empty<object>() });
        removed.EnsureSuccessStatusCode();
        var empty = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{id}");
        Assert.Empty(empty.GetProperty("tags").EnumerateArray());
    }

    [Fact]
    public async Task Invalid_or_stale_tag_changes_leave_the_reference_unchanged_and_foreign_reads_are_unavailable()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var uploaded = await ReferenceFixture.SubmitAsync(owner);
        var id = (await uploaded.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var request = new { revision = 1, tags = new[] { new { name = "portrait", category = (string?)null } } };
        using var foreign = await stranger.PutAsJsonAsync($"/api/references/{id}/tags", request);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        foreach (var tags in new[] {
            new[] { new { name = "", category = (string?)null } },
            new[] { new { name = new string('x', 51), category = (string?)null } },
            new[] { new { name = "portrait", category = (string?)"unknown" } },
            Enumerable.Range(0, 51).Select(i => new { name = $"tag {i}", category = (string?)null }).ToArray() })
        {
            using var invalid = await owner.PutAsJsonAsync($"/api/references/{id}/tags", new { revision = 1, tags });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        using var saved = await owner.PutAsJsonAsync($"/api/references/{id}/tags", request);
        saved.EnsureSuccessStatusCode();
        using var stale = await owner.PutAsJsonAsync($"/api/references/{id}/tags", new { revision = 1, tags = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var result = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{id}");
        Assert.Single(result.GetProperty("tags").EnumerateArray());
    }
}
