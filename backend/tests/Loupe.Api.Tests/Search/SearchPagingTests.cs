using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;
using static Loupe.Api.Tests.Search.SearchFixture;

namespace Loupe.Api.Tests.Search;

public sealed class SearchPagingTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Given_tied_creation_times_when_paging_mixed_results_then_every_item_appears_once_in_ID_order()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var expected = new List<Guid>();
        for (var index = 0; index < 7; index++)
            expected.Add(index % 2 == 0
                ? await Save(owner, "references/links", new { title = "Tied", sourceUrl = $"https://tie.example/{index}" }, "reference")
                : await Save(owner, "photographers", new { name = "Tied", portfolioUrl = $"https://tie.example/{index}" }, "photographer"));
        var found = new List<Guid>();
        var creationTimes = new List<DateTimeOffset>();
        string? cursor = null;
        do
        {
            var page = await ReadSearch(owner, "query=tied&pageSize=2" + (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor)));
            Assert.Equal(7, page.GetProperty("totalCount").GetInt32());
            Assert.InRange(page.GetProperty("items").GetArrayLength(), 1, 2);
            found.AddRange(Ids(page));
            creationTimes.AddRange(page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("createdAt").GetDateTimeOffset()));
            Assert.True(found.Count <= expected.Count, "Paging must terminate without repeating results.");
            cursor = page.GetProperty("nextCursor").GetString();
        } while (cursor is not null);
        Assert.Equal(expected.Order(), found);
        Assert.Single(creationTimes.Distinct());
    }

    [Fact]
    public async Task Given_a_cursor_when_owner_or_one_search_parameter_changes_then_reject_reuse_with_a_cursor_error()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        for (var index = 0; index < 3; index++)
            await Save(owner, "references/links", new { title = "Scope", sourceUrl = $"https://scope.example/{index}" }, "reference");
        using var saved = await owner.PostAsJsonAsync("/api/boards", new { name = "Scope" });
        saved.EnsureSuccessStatusCode();
        var board = (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var page = await ReadSearch(owner, "query=scope&pageSize=1");
        var cursor = Uri.EscapeDataString(page.GetProperty("nextCursor").GetString()!);
        foreach (var changed in new[] { "query=different&pageSize=1", "query=scope&pageSize=2",
            "query=scope&pageSize=1&type=references", "query=scope&pageSize=1&tags=selected",
            $"query=scope&pageSize=1&boardIds={board}" })
            await CursorError(owner, changed + "&cursor=" + cursor);
        await CursorError(stranger, "query=scope&pageSize=1&cursor=" + cursor);
        Assert.Single(Ids(await ReadSearch(owner, "query=scope&pageSize=1&mode=keyword&cursor=" + cursor)));
    }

    [Fact]
    public async Task Given_a_valid_scope_when_the_cursor_payload_is_malformed_then_return_a_cursor_field_error()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        for (var index = 0; index < 2; index++)
            await Save(owner, "references/links", new { sourceUrl = $"https://cursor.example/{index}" }, "reference");
        var cursor = (await ReadSearch(owner, "pageSize=1")).GetProperty("nextCursor").GetString()!;
        var scope = cursor[..(cursor.IndexOf('.') + 1)];
        var id = Guid.NewGuid().ToString("N");
        foreach (var payload in new[] { "", "not-base64", Encode("bad:" + id), Encode("-1:" + id),
            Encode(long.MaxValue + ":" + id), Encode("0:invalid"), new string('x', 201) })
            await CursorError(owner, "pageSize=1&cursor=" + Uri.EscapeDataString(scope + payload));
    }

    private static string Encode(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

    private static async Task CursorError(HttpClient owner, string query)
    {
        using var response = await owner.GetAsync("/api/search?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotEmpty((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("cursor").EnumerateArray());
    }
}
