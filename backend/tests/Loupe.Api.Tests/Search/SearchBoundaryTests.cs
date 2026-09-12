using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using static Loupe.Api.Tests.Search.SearchFixture;

namespace Loupe.Api.Tests.Search;

public sealed class SearchBoundaryTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData("tags", "tags")]
    [InlineData("tags=", "tags")]
    [InlineData("tags=%20%09", "tags")]
    [InlineData("tags=valid&tags=", "tags")]
    [InlineData("tags[0]=", "tags")]
    [InlineData("tags=%00", "tags")]
    [InlineData("tags=abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz", "tags")]
    [InlineData("boardIds=not-a-guid", "boardIds")]
    [InlineData("boardIds=", "boardIds")]
    [InlineData("pageSize=not-a-number", "pageSize")]
    public async Task Given_malformed_filters_when_bound_by_the_API_then_return_a_field_error_not_a_server_error(string query, string field)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var response = await owner.GetAsync("/api/search?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEmpty(problem.GetProperty("errors").GetProperty(field).EnumerateArray());
    }

    [Theory]
    [InlineData("😀")]
    [InlineData("e\u0301")]
    public async Task Given_Unicode_queries_when_at_or_above_500_normalized_scalars_then_enforce_the_boundary(string scalar)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var text = string.Concat(Enumerable.Repeat(scalar, 500));
        var id = await Save(owner, "photographers", new { name = "Boundary", portfolioUrl = "https://boundary.example/", notes = text }, "photographer");
        Assert.Equal(new[] { id }, Ids(await ReadSearch(owner, "query=" + Uri.EscapeDataString(" \t" + text + "\r\n "))));
        using var oversized = await owner.GetAsync("/api/search?query=" + Uri.EscapeDataString(text + scalar));
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
        Assert.NotEmpty((await oversized.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("query").EnumerateArray());
        Assert.Equal(new[] { id }, Ids(await ReadSearch(owner, "query=%20%09%0A")));
    }

    [Fact]
    public async Task Given_ten_tags_and_boards_when_filtering_with_maximum_page_size_then_accept_valid_limits()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = await Save(owner, "references/links", new { title = "Boundary", sourceUrl = "https://boundary.example/" }, "reference");
        var tags = Enumerable.Range(0, 9).Select(index => "tag" + index).Append(new string('x', 50)).ToArray();
        await SetTags(owner, id, 1, tags);
        var boards = new List<Guid>();
        for (var index = 0; index < 10; index++)
        {
            using var created = await owner.PostAsJsonAsync("/api/boards", new { name = "Board " + index });
            created.EnsureSuccessStatusCode();
            boards.Add((await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        }
        using var assigned = await owner.PutAsJsonAsync($"/api/references/{id}/boards", new { revision = 2, boardIds = boards });
        assigned.EnsureSuccessStatusCode();
        var page = await ReadSearch(owner, "query=boundary&type=references&pageSize=100&"
            + string.Join('&', tags.Select(tag => "tags=" + tag).Concat(boards.Select(board => "boardIds=" + board))));
        Assert.Equal(new[] { id }, Ids(page));
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
    }
}
