using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using static Loupe.Api.Tests.Search.SearchFixture;

namespace Loupe.Api.Tests.Search;

public sealed class SearchMutationTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Given_pending_metadata_when_accepted_edited_removed_or_deleted_then_search_and_facets_immediately_follow_active_state()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var reference = await Save(owner, "references/links", new { title = "Earlier-title", sourceUrl = "https://earlier-host.example/", attribution = "Earlier-credit", notes = "Earlier-notes" }, "reference");
        var photographer = await Save(owner, "photographers", new { name = "Bookmark", portfolioUrl = "https://bookmark.example/" }, "photographer");
        var referenceOperation = await PendingReferenceSuggestions(factory, reference, "reviewed-description", "reviewed-tag");
        var photographerOperation = await PendingPhotographerSuggestions(factory, photographer, "reviewed-summary", "reviewed-tag");
        await Matches(owner, "query=reviewed");
        using var acceptReference = await owner.PutAsJsonAsync($"/api/references/{reference}/suggestions",
            new { revision = 1, operationId = referenceOperation, target = "all", decision = "accept" });
        acceptReference.EnsureSuccessStatusCode();
        using var acceptPhotographer = await owner.PutAsJsonAsync($"/api/photographers/{photographer}/suggestions",
            new { revision = 1, operationId = photographerOperation, target = "all", decision = "accept" });
        acceptPhotographer.EnsureSuccessStatusCode();
        await Matches(owner, "query=reviewed-description", reference);
        await Matches(owner, "query=reviewed-summary", photographer);
        await Matches(owner, "query=reviewed-tag&tags=REVIEWED-TAG", reference, photographer);
        var facets = await owner.GetFromJsonAsync<JsonElement>("/api/search/tags");
        Assert.Equal(2, Assert.Single(facets.EnumerateArray()).GetProperty("count").GetInt32());

        await SetTags(owner, reference, 2);
        await Matches(owner, "tags=reviewed-tag", photographer);
        using var editedPhotographer = await owner.PutAsJsonAsync($"/api/photographers/{photographer}", new
        {
            revision = 2, name = "Bookmark", portfolioUrl = "https://bookmark.example/",
            summary = "revised-summary", notes = "revised-bookmark-notes", tags = Array.Empty<object>()
        });
        editedPhotographer.EnsureSuccessStatusCode();
        await Matches(owner, "query=reviewed-tag");
        await Matches(owner, "tags=reviewed-tag");
        await Matches(owner, "query=reviewed-summary");
        await Matches(owner, "query=revised-summary%20revised-bookmark-notes", photographer);
        Assert.Empty((await owner.GetFromJsonAsync<JsonElement>("/api/search/tags")).EnumerateArray());

        using var editedReference = await owner.PutAsJsonAsync($"/api/references/{reference}", new
        {
            revision = 3, title = "Revised-title", sourceUrl = "https://revised-host.example/",
            attribution = "Revised-credit", notes = "Revised-notes"
        });
        editedReference.EnsureSuccessStatusCode();
        using var editedDescription = await owner.PutAsJsonAsync($"/api/references/{reference}/description", new { revision = 4, text = "Revised-description" });
        editedDescription.EnsureSuccessStatusCode();
        foreach (var removed in new[] { "earlier-title", "earlier-host", "earlier-credit", "earlier-notes", "reviewed-description" })
            await Matches(owner, "query=" + removed);
        await Matches(owner, "query=revised-title%20revised-host%20revised-credit%20revised-notes%20revised-description", reference);
        using var deleted = await owner.DeleteAsync($"/api/photographers/{photographer}?revision=3");
        deleted.EnsureSuccessStatusCode();
        await Matches(owner, "query=revised-summary");
        await Matches(owner, "type=photographers");
        await Matches(owner, "", reference);
    }

    [Fact]
    public async Task Given_a_board_filter_when_membership_changes_or_the_board_is_deleted_then_counts_update_without_broadening_results()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var reference = await Save(owner, "references/links", new { title = "Study", sourceUrl = "https://study.example/" }, "reference");
        await SetTags(owner, reference, 1, "board-tag");
        using var created = await owner.PostAsJsonAsync("/api/boards", new { name = "Private board name" });
        created.EnsureSuccessStatusCode();
        var board = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var query = $"query=study&tags=board-tag&type=references&boardIds={board}";
        await Matches(owner, query);
        using var added = await owner.PutAsJsonAsync($"/api/references/{reference}/boards", new { revision = 2, boardIds = new[] { board } });
        added.EnsureSuccessStatusCode();
        await Matches(owner, query, reference);
        using var removed = await owner.PutAsJsonAsync($"/api/references/{reference}/boards", new { revision = 3, boardIds = Array.Empty<Guid>() });
        removed.EnsureSuccessStatusCode();
        await Matches(owner, query);
        using var restored = await owner.PutAsJsonAsync($"/api/references/{reference}/boards", new { revision = 4, boardIds = new[] { board } });
        restored.EnsureSuccessStatusCode();
        await Matches(owner, query, reference);
        using var deleted = await owner.DeleteAsync($"/api/boards/{board}?revision=1");
        deleted.EnsureSuccessStatusCode();
        using var unavailable = await owner.GetAsync("/api/search?" + query);
        Assert.Equal(HttpStatusCode.NotFound, unavailable.StatusCode);
        var problem = await unavailable.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("item_unavailable", problem.GetProperty("code").GetString());
        Assert.DoesNotContain("Private board name", problem.ToString());
        await Matches(owner, "query=study&tags=board-tag&type=references", reference);
    }

    private static async Task Matches(HttpClient owner, string query, params Guid[] expected)
    {
        var page = await ReadSearch(owner, query);
        Assert.Equal(expected.Order(), Ids(page).Order());
        Assert.Equal(expected.Length, page.GetProperty("totalCount").GetInt32());
    }
}
