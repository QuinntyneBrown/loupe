using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.References;

public sealed class DeleteReferenceTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Deleting_removes_reference_memberships_tags_and_media_access_but_keeps_boards_and_other_references()
    {
        var subject = Guid.NewGuid().ToString();
        Guid id;
        await using (var factory = new ApiFactory(database.ConnectionString, database.MediaRoot))
        {
            using var owner = await factory.CreateAuthenticatedClientAsync(subject);
            using var upload = await ReferenceFixture.SubmitAsync(owner);
            id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            using var other = await ReferenceFixture.SubmitAsync(owner);
            using var created = await owner.PostAsJsonAsync("/api/boards", new { name = "Keep this board" });
            var board = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            using var membership = await owner.PutAsJsonAsync($"/api/references/{id}/boards", new { revision = 1, boardIds = new[] { board } }); membership.EnsureSuccessStatusCode();
            using var tags = await owner.PutAsJsonAsync($"/api/references/{id}/tags", new { revision = 2, tags = new[] { new { name = "temporary", category = "subject" } } }); tags.EnsureSuccessStatusCode();
            using var deleted = await owner.DeleteAsync($"/api/references/{id}?revision=3"); Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
            foreach (var suffix in new[] { "", "/image", "/preview" })
            { using var missing = await owner.GetAsync($"/api/references/{id}{suffix}"); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode); }
            Assert.Single((await owner.GetFromJsonAsync<JsonElement>("/api/references")).GetProperty("items").EnumerateArray());
            Assert.Empty((await owner.GetFromJsonAsync<JsonElement>("/api/references/tags")).EnumerateArray());
            var keptBoard = Assert.Single((await owner.GetFromJsonAsync<JsonElement>("/api/boards")).EnumerateArray());
            Assert.Equal(board, keptBoard.GetProperty("id").GetGuid()); Assert.Equal(0, keptBoard.GetProperty("referenceCount").GetInt32());
        }
        await using var restart = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var later = await restart.CreateAuthenticatedClientAsync(subject);
        using var replay = await later.DeleteAsync($"/api/references/{id}?revision=3"); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
    }

    [Fact]
    public async Task Foreign_and_stale_deletions_preserve_the_saved_reference()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var upload = await ReferenceFixture.SubmitAsync(owner); var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var before = await owner.GetStringAsync($"/api/references/{id}");
        using var foreign = await stranger.DeleteAsync($"/api/references/{id}?revision=1"); Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var stale = await owner.DeleteAsync($"/api/references/{id}?revision=2"); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var invalid = await owner.DeleteAsync($"/api/references/{id}?revision=0"); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(before, await owner.GetStringAsync($"/api/references/{id}"));
    }

    [Fact]
    public async Task Deleting_cancels_admitted_source_work()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot) { Settings = new Dictionary<string, string?> { ["Imports:Mode"] = "Live" } };
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var upload = await ReferenceFixture.SubmitAsync(owner, new Dictionary<string, string> { ["sourceUrl"] = "https://source.example/photo" });
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/references/{id}/imports") { Content = JsonContent.Create(new { revision = 1 }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var admission = await owner.SendAsync(request); admission.EnsureSuccessStatusCode();
        using var deleted = await owner.DeleteAsync($"/api/references/{id}?revision=1"); Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        var operation = await owner.GetFromJsonAsync<JsonElement>(admission.Headers.Location);
        Assert.Equal("Canceled", operation.GetProperty("status").GetString());
    }
}
