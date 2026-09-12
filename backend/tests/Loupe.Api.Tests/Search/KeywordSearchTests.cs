using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.Search;

public sealed class KeywordSearchTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Keyword_tokens_match_across_active_fields_with_NFC_and_literal_substrings_privately()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var photographer = await Save(owner, "photographers", new { name = "Café Casey", portfolioUrl = "https://window.example/secret-path", summary = "Portraits", notes = "100% soft light", tags = new[] { new { name = "warm", category = "palette" } } }, "photographer");
        var reference = await Save(owner, "references/links", new { title = "Portraits", sourceUrl = "https://window.example/secret-path", notes = "100% soft light", attribution = "Café Casey" }, "reference");
        await Save(stranger, "references/links", new { title = "Portraits Café Casey", sourceUrl = "https://window.example/", notes = "100% soft light" }, "reference");
        var found = await Search(owner, "query=" + Uri.EscapeDataString("CAFE\u0301 portraits soft 100% window"));
        Assert.Equal(new[] { photographer, reference }.Order(), Ids(found).Order());
        Assert.All(found.GetProperty("items").EnumerateArray(), item => Assert.StartsWith("https://window.example/", item.GetProperty("sourceUrl").GetString()));
        Assert.Equal(0, (await Search(owner, "query=secret-path")).GetProperty("totalCount").GetInt32());
        Assert.Equal(0, (await Search(owner, "query=portraits%20absent")).GetProperty("totalCount").GetInt32());
        Assert.Equal(new[] { photographer }, Ids(await Search(owner, "query=warm")));
        using var anonymous = factory.CreateClient(); using var denied = await anonymous.GetAsync("/api/search"); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [Fact]
    public async Task Filters_combine_tags_AND_boards_OR_and_type_before_stable_paging()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var boards = new List<Guid>();
        foreach (var name in new[] { "Board A", "Board B" }) { using var saved = await owner.PostAsJsonAsync("/api/boards", new { name }); saved.EnsureSuccessStatusCode(); boards.Add((await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid()); }
        var expected = new List<Guid>();
        for (var index = 0; index < 4; index++)
        {
            var id = await Save(owner, "references/links", new { title = "Study", sourceUrl = $"https://study.example/{index}" }, "reference");
            using var tags = await owner.PutAsJsonAsync($"/api/references/{id}/tags", new { revision = 1, tags = index == 3 ? new[] { new { name = "A", category = "subject" } } : new[] { new { name = "A", category = "subject" }, new { name = "B", category = "mood" } } }); tags.EnsureSuccessStatusCode();
            using var assigned = await owner.PutAsJsonAsync($"/api/references/{id}/boards", new { revision = 2, boardIds = index == 0 ? boards.ToArray() : new[] { boards[index % 2] } }); assigned.EnsureSuccessStatusCode();
            if (index < 3) expected.Add(id);
            factory.Clock.Advance(TimeSpan.FromSeconds(1));
        }
        await Save(owner, "photographers", new { name = "Study", portfolioUrl = "https://study.example/", tags = new[] { new { name = "A", category = "subject" }, new { name = "B", category = "mood" } } }, "photographer");
        var query = $"query=study&tags=a&tags=b&boardIds={boards[0]}&boardIds={boards[1]}&pageSize=2";
        var first = await Search(owner, query); Assert.Equal(3, first.GetProperty("totalCount").GetInt32()); Assert.Equal(expected.AsEnumerable().Reverse().Take(2), Ids(first));
        var cursor = Uri.EscapeDataString(first.GetProperty("nextCursor").GetString()!);
        var second = await Search(owner, query + "&cursor=" + cursor); Assert.Equal(new[] { expected[0] }, Ids(second)); Assert.Equal(JsonValueKind.Null, second.GetProperty("nextCursor").ValueKind);
        Assert.Equal(0, (await Search(owner, query + "&type=photographers")).GetProperty("totalCount").GetInt32());
        Assert.Equal(1, (await Search(owner, "tags=a&tags=b&type=photographers")).GetProperty("totalCount").GetInt32());
        Assert.Equal(0, (await Search(owner, "query=Board%20A")).GetProperty("totalCount").GetInt32());
        using var foreign = await stranger.GetAsync($"/api/search?boardIds={boards[0]}"); Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var changed = await owner.GetAsync("/api/search?pageSize=2&cursor=" + cursor); Assert.Equal(HttpStatusCode.BadRequest, changed.StatusCode);
    }

    [Theory]
    [InlineData("type=invalid")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("query=%00")]
    [InlineData("cursor=invalid")]
    public async Task Invalid_search_is_a_field_error(string query)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var response = await owner.GetAsync("/api/search?" + query); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var oversized = await owner.GetAsync("/api/search?query=" + new string('x', 501)); Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
    }

    [Fact]
    public async Task Search_cards_include_linked_reference_counts_and_immediately_reflect_renames_and_unlinking()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var photographer = await Save(owner, "photographers", new { name = "Casey", portfolioUrl = "https://casey.example/" }, "photographer");
        var reference = await Save(owner, "references/links", new { title = "Study", sourceUrl = "https://study.example/", attribution = "Original credit" }, "reference");
        using var linked = await owner.PutAsJsonAsync($"/api/references/{reference}/photographer", new { revision = 1, photographerId = photographer }); linked.EnsureSuccessStatusCode();
        var card = Assert.Single((await Search(owner, "type=photographers")).GetProperty("items").EnumerateArray());
        Assert.Equal(1, card.GetProperty("referenceCount").GetInt32());
        Assert.Empty(card.GetProperty("referencePreviewUrls").EnumerateArray());
        using var renamed = await owner.PutAsJsonAsync($"/api/photographers/{photographer}", new { revision = 1, name = "Robin", portfolioUrl = "https://casey.example/", notes = "New direction" }); renamed.EnsureSuccessStatusCode();
        Assert.Equal(new[] { reference }, Ids(await Search(owner, "query=robin&type=references")));
        Assert.Equal(new[] { photographer }, Ids(await Search(owner, "query=new%20direction")));
        using var unlinked = await owner.PutAsJsonAsync($"/api/references/{reference}/photographer", new { revision = 2, photographerId = (Guid?)null }); unlinked.EnsureSuccessStatusCode();
        Assert.Empty(Ids(await Search(owner, "query=robin&type=references")));
        Assert.Equal(0, (await Search(owner, "type=photographers")).GetProperty("items")[0].GetProperty("referenceCount").GetInt32());
        using var deleted = await owner.DeleteAsync($"/api/references/{reference}?revision=3"); deleted.EnsureSuccessStatusCode();
        Assert.Empty(Ids(await Search(owner, "type=references")));
        using var tooManyTags = await owner.GetAsync("/api/search?" + string.Join('&', Enumerable.Range(0, 11).Select(i => "tags=t" + i))); Assert.Equal(HttpStatusCode.BadRequest, tooManyTags.StatusCode);
        using var tooManyBoards = await owner.GetAsync("/api/search?" + string.Join('&', Enumerable.Range(0, 11).Select(_ => "boardIds=" + Guid.NewGuid()))); Assert.Equal(HttpStatusCode.BadRequest, tooManyBoards.StatusCode);
        Assert.Empty(Ids(await Search(owner, "tags=unknown")));
    }

    private static async Task<JsonElement> Search(HttpClient owner, string query) { using var response = await owner.GetAsync("/api/search?" + query); Assert.Equal(HttpStatusCode.OK, response.StatusCode); return await response.Content.ReadFromJsonAsync<JsonElement>(); }
    private static Guid[] Ids(JsonElement page) => page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray();
    private static async Task<Guid> Save(HttpClient owner, string route, object input, string property)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/" + route) { Content = JsonContent.Create(input) }; request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await owner.SendAsync(request); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty(property).GetProperty("id").GetGuid();
    }
}
