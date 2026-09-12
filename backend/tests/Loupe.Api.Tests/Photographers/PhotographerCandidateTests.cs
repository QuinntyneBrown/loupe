using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.Photographers;

public sealed class PhotographerCandidateTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Candidate_search_pages_all_owned_references_and_returns_current_link_revisions()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var photographer = await Save(owner, "/api/photographers", new { name = "Casey", portfolioUrl = "https://casey.example/" }, "photographer");
        var references = new List<Guid>();
        for (var index = 0; index < 26; index++)
        {
            references.Add(await Save(owner, "/api/references/links", new { title = index < 2 ? $"100% Window {index}" : $"Study {index}", sourceUrl = $"https://reference.example/{index}" }, "reference"));
            factory.Clock.Advance(TimeSpan.FromSeconds(1));
        }
        using var linked = await owner.PutAsJsonAsync($"/api/references/{references[0]}/photographer", new { photographerId = photographer, revision = 1 }); linked.EnsureSuccessStatusCode();
        await Save(stranger, "/api/references/links", new { title = "100% Window private", sourceUrl = "https://private.example/" }, "reference");
        var route = $"/api/photographers/{photographer}/reference-candidates";
        using var response = await owner.GetAsync(route); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var first = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(26, first.GetProperty("totalCount").GetInt32()); Assert.Equal(24, first.GetProperty("items").GetArrayLength());
        var cursor = Uri.EscapeDataString(first.GetProperty("nextCursor").GetString()!);
        var second = await owner.GetFromJsonAsync<JsonElement>($"{route}?cursor={cursor}"); Assert.Equal(2, second.GetProperty("items").GetArrayLength());
        var candidate = second.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == references[0]);
        Assert.Equal(2, candidate.GetProperty("revision").GetInt64()); Assert.Equal("Casey", candidate.GetProperty("photographer").GetProperty("name").GetString());
        var search = await owner.GetFromJsonAsync<JsonElement>($"{route}?query=100%25%20window"); Assert.Equal(2, search.GetProperty("totalCount").GetInt32());
        Assert.Equal(references.Take(2).Reverse(), search.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
        var literal = await owner.GetFromJsonAsync<JsonElement>($"{route}?query=%25"); Assert.Equal(2, literal.GetProperty("totalCount").GetInt32());
        using var foreign = await stranger.GetAsync(route); Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var wrongQuery = await owner.GetAsync($"{route}?query=window&cursor={cursor}"); Assert.Equal(HttpStatusCode.BadRequest, wrongQuery.StatusCode);
        using var wrongSize = await owner.GetAsync($"{route}?pageSize=2&cursor={cursor}"); Assert.Equal(HttpStatusCode.BadRequest, wrongSize.StatusCode);
        using var oversized = await owner.GetAsync($"{route}?query={new string('x', 201)}"); Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
    }

    private static async Task<Guid> Save(HttpClient owner, string route, object input, string property)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(input) }; request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await owner.SendAsync(request); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty(property).GetProperty("id").GetGuid();
    }
}
