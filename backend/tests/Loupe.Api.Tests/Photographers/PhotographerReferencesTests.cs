using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.Photographers;

public sealed class PhotographerReferencesTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Collection_counts_and_detail_pages_follow_current_links_without_duplicates()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var photographer = await Save(owner, "/api/photographers", new { name = "Casey", portfolioUrl = "https://casey.example/" }, "photographer");
        var ids = new List<Guid>();
        for (var index = 0; index < 25; index++)
        {
            var id = await Save(owner, "/api/references/links", new { title = $"Reference {index}", sourceUrl = $"https://reference.example/{index}" }, "reference"); ids.Add(id);
            using var link = await owner.PutAsJsonAsync($"/api/references/{id}/photographer", new { photographerId = photographer, revision = 1 }); link.EnsureSuccessStatusCode();
            factory.Clock.Advance(TimeSpan.FromSeconds(1));
        }
        using var response = await owner.GetAsync($"/api/photographers/{photographer}/references"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var first = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(25, first.GetProperty("totalCount").GetInt32());
        Assert.Equal(ids.AsEnumerable().Reverse().Take(24), first.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
        var cursor = Uri.EscapeDataString(first.GetProperty("nextCursor").GetString()!);
        var last = await owner.GetFromJsonAsync<JsonElement>($"/api/photographers/{photographer}/references?cursor={cursor}");
        Assert.Equal(ids[0], Assert.Single(last.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid()); Assert.Equal(JsonValueKind.Null, last.GetProperty("nextCursor").ValueKind);
        using var foreign = await stranger.GetAsync($"/api/photographers/{photographer}/references"); Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        var collection = await owner.GetFromJsonAsync<JsonElement>("/api/photographers");
        var card = Assert.Single(collection.GetProperty("items").EnumerateArray()); Assert.Equal(25, card.GetProperty("referenceCount").GetInt32());
        Assert.Equal(ids.AsEnumerable().Reverse().Take(3), card.GetProperty("references").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
        using var unlink = await owner.PutAsJsonAsync($"/api/references/{ids[24]}/photographer", new { photographerId = (Guid?)null, revision = 2 }); unlink.EnsureSuccessStatusCode();
        var refreshed = await owner.GetFromJsonAsync<JsonElement>($"/api/photographers/{photographer}/references"); Assert.Equal(24, refreshed.GetProperty("totalCount").GetInt32());
        Assert.DoesNotContain(refreshed.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == ids[24]);
        using var badSize = await owner.GetAsync($"/api/photographers/{photographer}/references?pageSize=101"); Assert.Equal(HttpStatusCode.BadRequest, badSize.StatusCode);
        using var badCursor = await owner.GetAsync($"/api/photographers/{photographer}/references?pageSize=5&cursor={cursor}"); Assert.Equal(HttpStatusCode.BadRequest, badCursor.StatusCode);
    }

    private static async Task<Guid> Save(HttpClient owner, string route, object input, string property)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(input) }; request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await owner.SendAsync(request); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty(property).GetProperty("id").GetGuid();
    }
}
