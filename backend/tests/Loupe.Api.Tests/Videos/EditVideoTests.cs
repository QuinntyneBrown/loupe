using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.Videos;

// L2-055.3, L2-055.4, L2-055.5: edits replace fields with the opened revision, URL changes deduplicate, and deletion removes the bookmark.
public sealed class EditVideoTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Given_a_video_when_edited_with_its_revision_then_fields_and_tags_are_replaced_and_stale_edits_conflict()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var marker = Guid.NewGuid().ToString("N")[..11]; var replacement = Guid.NewGuid().ToString("N")[..11];
        var saved = await VideoFixture.SaveOk(owner, new
        {
            title = "Before",
            url = "https://youtu.be/" + marker,
            topic = "posing",
            notes = "Keep",
            tags = new object[] { new { name = "hands", category = "technique" }, new { name = "old" } }
        });
        var id = saved.GetProperty("id").GetGuid();
        using var edited = await owner.PutAsJsonAsync($"/api/videos/{id}", new
        {
            revision = 1,
            title = "After",
            url = "https://www.youtube.com/watch?v=" + replacement,
            topic = "lighting",
            channel = "Studio",
            summary = "Rembrandt light",
            notes = (string?)null,
            tags = new object[] { new { name = "Hands", category = "lighting" }, new { name = "new" } }
        });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var result = await edited.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("After", result.GetProperty("title").GetString()); Assert.Equal(replacement, result.GetProperty("videoId").GetString());
        Assert.Equal("lighting", result.GetProperty("topic").GetString()); Assert.Equal("Studio", result.GetProperty("channel").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("notes").ValueKind); Assert.Equal(2, result.GetProperty("revision").GetInt64());
        Assert.Equal(["Hands", "new"], result.GetProperty("tags").EnumerateArray().Select(tag => tag.GetProperty("name").GetString()));
        Assert.Equal("lighting", result.GetProperty("tags").EnumerateArray().First().GetProperty("category").GetString());
        using var stale = await owner.PutAsJsonAsync($"/api/videos/{id}", new { revision = 1, title = "Stale", url = "https://youtu.be/" + replacement, topic = "posing" });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("revision_conflict", (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal("After", (await owner.GetFromJsonAsync<JsonElement>($"/api/videos/{id}")).GetProperty("title").GetString());
        using var invalid = await owner.PutAsJsonAsync($"/api/videos/{id}", new { revision = 2, title = "", url = "https://youtu.be/" + replacement, topic = "posing" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var foreign = await stranger.PutAsJsonAsync($"/api/videos/{id}", new { revision = 2, title = "Taken", url = "https://youtu.be/" + replacement, topic = "posing" });
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task Given_another_bookmark_with_the_target_video_when_the_url_is_changed_then_a_conflict_preserves_both()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var first = Guid.NewGuid().ToString("N")[..11]; var second = Guid.NewGuid().ToString("N")[..11];
        await VideoFixture.SaveOk(owner, new { title = "First", url = "https://youtu.be/" + first, topic = "posing" });
        var id = (await VideoFixture.SaveOk(owner, new { title = "Second", url = "https://youtu.be/" + second, topic = "posing" })).GetProperty("id").GetGuid();
        using var conflict = await owner.PutAsJsonAsync($"/api/videos/{id}", new { revision = 1, title = "Second", url = "https://youtu.be/" + first, topic = "posing" });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("video_conflict", (await conflict.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        var kept = await owner.GetFromJsonAsync<JsonElement>($"/api/videos/{id}");
        Assert.Equal(second, kept.GetProperty("videoId").GetString()); Assert.Equal(1, kept.GetProperty("revision").GetInt64());
        Assert.Equal(2, (await VideoFixture.List(owner)).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Given_a_video_when_deleted_with_its_revision_then_it_is_gone_and_stale_or_foreign_deletes_fail()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await VideoFixture.SaveOk(owner, new { title = "Gone soon", url = "https://youtu.be/" + Guid.NewGuid().ToString("N")[..11], topic = "gear", tags = new[] { new { name = "tripod" } } })).GetProperty("id").GetGuid();
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var foreign = await stranger.DeleteAsync($"/api/videos/{id}?revision=1"); Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var stale = await owner.DeleteAsync($"/api/videos/{id}?revision=2"); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var deleted = await owner.DeleteAsync($"/api/videos/{id}?revision=1"); Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var missing = await owner.GetAsync($"/api/videos/{id}"); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var again = await owner.DeleteAsync($"/api/videos/{id}?revision=1"); Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Empty(await owner.GetFromJsonAsync<JsonElement[]>("/api/videos/tags") ?? []);
    }
}
