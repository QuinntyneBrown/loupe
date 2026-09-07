// Given saved private photographs, when their owner deletes them, then access is
// revoked atomically with a durable, repeatable pending-cleanup operation.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class DeletePhotographTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_031_1_2_6_Deletion_revokes_reads_and_edits_but_reports_physical_cleanup_as_pending()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var photo = await PhotographFixture.UploadAsync(client);
        var id = photo.GetProperty("id").GetGuid();
        using var notes = await client.PutAsJsonAsync($"/api/photographs/{id}/notes", new { revision = 1, notes = "Private study notes" });
        notes.EnsureSuccessStatusCode();
        var fileCount = Directory.GetFiles(database.MediaRoot).Length;
        using var deleted = await client.DeleteAsync($"/api/photographs/{id}?revision=2");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        var operation = await deleted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, operation.GetProperty("resourceId").GetGuid());
        Assert.Equal("Pending", operation.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, operation.GetProperty("completedAt").ValueKind);
        using var status = await client.GetAsync($"/api/deletions/{operation.GetProperty("id").GetGuid()}");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.Equal(operation.GetRawText(), (await status.Content.ReadFromJsonAsync<JsonElement>()).GetRawText());
        foreach (var suffix in new[] { "", "/image", "/preview" })
        {
            using var unavailable = await client.GetAsync($"/api/photographs/{id}{suffix}");
            Assert.Equal(HttpStatusCode.NotFound, unavailable.StatusCode);
        }
        using var edit = await client.PutAsJsonAsync($"/api/photographs/{id}/notes", new { revision = 2, notes = "Must not restore" });
        Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
        var list = await client.GetFromJsonAsync<JsonElement>("/api/photographs");
        Assert.Empty(list.GetProperty("items").EnumerateArray());
        Assert.Equal(fileCount, Directory.GetFiles(database.MediaRoot).Length);
    }

    [Fact]
    public async Task L2_031_5_Concurrent_deletion_and_restart_resolve_the_same_operation()
    {
        var subject = Guid.NewGuid().ToString();
        Guid id;
        string original;
        await using (var first = new ApiFactory(database.ConnectionString, database.MediaRoot))
        await using (var second = new ApiFactory(database.ConnectionString, database.MediaRoot))
        {
            using var a = await first.CreateAuthenticatedClientAsync(subject);
            using var b = await second.CreateAuthenticatedClientAsync(subject);
            id = (await PhotographFixture.UploadAsync(a)).GetProperty("id").GetGuid();
            var responses = await Task.WhenAll(a.DeleteAsync($"/api/photographs/{id}?revision=1"), b.DeleteAsync($"/api/photographs/{id}?revision=1"));
            try
            {
                foreach (var response in responses) Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                original = await responses[0].Content.ReadAsStringAsync();
                Assert.Equal(original, await responses[1].Content.ReadAsStringAsync());
            }
            finally { foreach (var response in responses) response.Dispose(); }
        }
        await using var restarted = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var later = await restarted.CreateAuthenticatedClientAsync(subject);
        using var repeated = await later.DeleteAsync($"/api/photographs/{id}?revision=1");
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.Equal(original, await repeated.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task L2_031_5_Foreign_and_unknown_deletions_and_statuses_are_unavailable()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(owner)).GetProperty("id").GetGuid();
        foreach (var target in new[] { id, Guid.NewGuid() })
        {
            using var hidden = await stranger.DeleteAsync($"/api/photographs/{target}?revision=1");
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
            var problem = await hidden.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("item_unavailable", problem.GetProperty("code").GetString());
        }
        using var visible = await owner.GetAsync($"/api/photographs/{id}");
        Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
        using var deleted = await owner.DeleteAsync($"/api/photographs/{id}?revision=1");
        deleted.EnsureSuccessStatusCode();
        var operation = await deleted.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var target in new[] { operation.GetProperty("id").GetGuid(), Guid.NewGuid() })
        {
            using var hidden = await stranger.GetAsync($"/api/deletions/{target}");
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        }
        using var repeat = await stranger.DeleteAsync($"/api/photographs/{id}?revision=1");
        Assert.Equal(HttpStatusCode.NotFound, repeat.StatusCode);
    }

    [Fact]
    public async Task L2_029_2_Stale_deletion_requires_the_latest_revision()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var updated = await client.PutAsJsonAsync($"/api/photographs/{id}/notes", new { revision = 1, notes = "New notes" });
        updated.EnsureSuccessStatusCode();
        using var conflict = await client.DeleteAsync($"/api/photographs/{id}?revision=1");
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var problem = await conflict.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("revision_conflict", problem.GetProperty("code").GetString());
        var latest = await client.GetFromJsonAsync<JsonElement>($"/api/photographs/{id}");
        Assert.Equal("New notes", latest.GetProperty("notes").GetString());
        using var deleted = await client.DeleteAsync($"/api/photographs/{id}?revision=2");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task L2_029_2_Invalid_deletion_revision_has_no_effect(long revision)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var invalid = await client.DeleteAsync($"/api/photographs/{id}?revision={revision}");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var visible = await client.GetAsync($"/api/photographs/{id}");
        Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
    }
}
