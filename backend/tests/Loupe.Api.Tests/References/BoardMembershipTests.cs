// Acceptance tests. Traces to L2-017, L2-018, L2-019, L2-030, L2-037.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.References;

public sealed class BoardMembershipTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_018_Board_pages_count_the_entire_board_and_reject_a_cursor_for_another_view()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var boardId = (await CreateBoard(owner, "Studies")).GetProperty("id").GetGuid();
        for (var index = 0; index < 3; index++)
        {
            var id = (await CreateReference(owner)).GetProperty("id").GetGuid();
            if (index == 2) continue;
            using var membership = await owner.PutAsJsonAsync($"/api/references/{id}/boards", new { revision = 1, boardIds = new[] { boardId } });
            membership.EnsureSuccessStatusCode();
        }
        var page = await owner.GetFromJsonAsync<JsonElement>($"/api/references?boardId={boardId}&pageSize=1");
        Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, page.GetProperty("libraryCount").GetInt32());
        var cursor = Uri.EscapeDataString(page.GetProperty("nextCursor").GetString()!);
        var next = await owner.GetFromJsonAsync<JsonElement>($"/api/references?boardId={boardId}&pageSize=1&cursor={cursor}");
        Assert.Single(next.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, next.GetProperty("nextCursor").ValueKind);
        Assert.NotEqual(page.GetProperty("items")[0].GetProperty("id").GetGuid(), next.GetProperty("items")[0].GetProperty("id").GetGuid());
        using var wrongView = await owner.GetAsync($"/api/references?pageSize=1&cursor={cursor}");
        Assert.Equal(HttpStatusCode.BadRequest, wrongView.StatusCode);
    }

    [Fact]
    public async Task L2_030_Conflicting_edits_leave_names_and_memberships_unchanged()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var a = (await CreateBoard(owner, "First")).GetProperty("id").GetGuid();
        var b = (await CreateBoard(owner, "Second")).GetProperty("id").GetGuid();
        using var duplicate = await owner.PutAsJsonAsync($"/api/boards/{a}", new { name = " SECOND ", revision = 1 });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var id = (await CreateReference(owner)).GetProperty("id").GetGuid();
        using var assigned = await owner.PutAsJsonAsync($"/api/references/{id}/boards", new { revision = 1, boardIds = new[] { a } });
        assigned.EnsureSuccessStatusCode();
        using var stale = await owner.PutAsJsonAsync($"/api/references/{id}/boards", new { revision = 1, boardIds = new[] { b } });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var reference = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{id}");
        Assert.Equal(a, Assert.Single(reference.GetProperty("boardIds").EnumerateArray()).GetGuid());
    }

    private static async Task<JsonElement> CreateBoard(HttpClient owner, string name)
    {
        using var response = await owner.PostAsJsonAsync("/api/boards", new { name });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> CreateReference(HttpClient owner)
    {
        using var response = await ReferenceFixture.SubmitAsync(owner);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task L2_018_Membership_replacement_is_atomic_idempotent_and_preserves_the_reference()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var a = (await CreateBoard(owner, "Window light")).GetProperty("id").GetGuid();
        var b = (await CreateBoard(owner, "Portrait sittings")).GetProperty("id").GetGuid();
        var reference = await CreateReference(owner);
        var id = reference.GetProperty("id").GetGuid();
        using var assigned = await owner.PutAsJsonAsync($"/api/references/{id}/boards", new { revision = 1, boardIds = new[] { a, b } });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        var result = await assigned.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, result.GetProperty("boardIds").GetArrayLength());
        var revision = result.GetProperty("revision").GetInt64();
        using var repeated = await owner.PutAsJsonAsync($"/api/references/{id}/boards", new { revision, boardIds = new[] { b, a, a } });
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.Equal(revision, (await repeated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revision").GetInt64());
        var boards = await owner.GetFromJsonAsync<JsonElement>("/api/boards");
        Assert.All(boards.EnumerateArray(), board => Assert.Equal(1, board.GetProperty("referenceCount").GetInt32()));
        using var removed = await owner.PutAsJsonAsync($"/api/references/{id}/boards", new { revision, boardIds = new[] { b } });
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        var saved = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{id}");
        Assert.Equal(b, Assert.Single(saved.GetProperty("boardIds").EnumerateArray()).GetGuid());
        Assert.Equal(reference.GetProperty("imageUrl").GetString(), saved.GetProperty("imageUrl").GetString());
        var all = await owner.GetFromJsonAsync<JsonElement>("/api/references");
        Assert.Single(all.GetProperty("items").EnumerateArray());
        Assert.Equal(1, all.GetProperty("totalCount").GetInt32());
        var empty = await owner.GetFromJsonAsync<JsonElement>($"/api/references?boardId={a}");
        Assert.Empty(empty.GetProperty("items").EnumerateArray());
        Assert.Equal(0, empty.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task L2_017_019_Rename_keeps_memberships_and_delete_preserves_references()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var board = await CreateBoard(owner, "Window light");
        var id = board.GetProperty("id").GetGuid();
        var referenceId = (await CreateReference(owner)).GetProperty("id").GetGuid();
        using var membership = await owner.PutAsJsonAsync($"/api/references/{referenceId}/boards", new { revision = 1, boardIds = new[] { id } });
        membership.EnsureSuccessStatusCode();
        using var renamed = await owner.PutAsJsonAsync($"/api/boards/{id}", new { name = "Soft light", revision = 1 });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var result = await renamed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Soft light", result.GetProperty("name").GetString());
        Assert.Equal(1, result.GetProperty("referenceCount").GetInt32());
        using var stale = await owner.DeleteAsync($"/api/boards/{id}?revision=1");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var deleted = await owner.DeleteAsync($"/api/boards/{id}?revision={result.GetProperty("revision").GetInt64()}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<JsonElement>("/api/boards")).EnumerateArray());
        var reference = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{referenceId}");
        Assert.Empty(reference.GetProperty("boardIds").EnumerateArray());
        using var missing = await owner.GetAsync($"/api/references?boardId={id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task L2_018_Foreign_board_assignment_makes_no_partial_changes()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var other = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var own = (await CreateBoard(owner, "Own")).GetProperty("id").GetGuid();
        var foreign = (await CreateBoard(other, "Foreign")).GetProperty("id").GetGuid();
        var id = (await CreateReference(owner)).GetProperty("id").GetGuid();
        using var denied = await owner.PutAsJsonAsync($"/api/references/{id}/boards", new { revision = 1, boardIds = new[] { own, foreign } });
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        var reference = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{id}");
        Assert.Empty(reference.GetProperty("boardIds").EnumerateArray());
        Assert.Equal(1, reference.GetProperty("revision").GetInt64());
        using var renamed = await owner.PutAsJsonAsync($"/api/boards/{foreign}", new { name = "Taken", revision = 1 });
        using var deleted = await owner.DeleteAsync($"/api/boards/{foreign}?revision=1");
        using var filtered = await owner.GetAsync($"/api/references?boardId={foreign}");
        Assert.Equal(HttpStatusCode.NotFound, renamed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, filtered.StatusCode);
    }
}
