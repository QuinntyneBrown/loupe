using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.Photographers;

public sealed class ListPhotographerTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Twenty_five_bookmarks_page_once_and_cursors_cannot_cross_owner_or_page_size()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var bookmarks = new List<(Guid Id, DateTimeOffset CreatedAt)>();
        for (var index = 0; index < 25; index++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/photographers") { Content = JsonContent.Create(new { name = $"Photographer {index}", portfolioUrl = $"https://portfolio.example/{index}" }) };
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            using var saved = await owner.SendAsync(request); saved.EnsureSuccessStatusCode();
            var bookmark = (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("photographer");
            bookmarks.Add((bookmark.GetProperty("id").GetGuid(), bookmark.GetProperty("createdAt").GetDateTimeOffset()));
            if (index % 2 == 0) factory.Clock.Advance(TimeSpan.FromSeconds(1));
        }
        using var response = await owner.GetAsync("/api/photographers"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var first = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(25, first.GetProperty("totalCount").GetInt32());
        var items = first.GetProperty("items").EnumerateArray().ToArray(); Assert.Equal(24, items.Length);
        var expected = bookmarks.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id).Select(item => item.Id).ToArray();
        Assert.Equal(expected.Take(24), items.Select(item => item.GetProperty("id").GetGuid()));
        Assert.All(items, item => { Assert.False(string.IsNullOrEmpty(item.GetProperty("name").GetString())); Assert.StartsWith("https://portfolio.example/", item.GetProperty("portfolioUrl").GetString()); });
        var cursor = Uri.EscapeDataString(first.GetProperty("nextCursor").GetString()!);
        var last = await owner.GetFromJsonAsync<JsonElement>($"/api/photographers?cursor={cursor}");
        Assert.Equal(expected[^1], Assert.Single(last.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, last.GetProperty("nextCursor").ValueKind);
        using var crossOwner = await stranger.GetAsync($"/api/photographers?cursor={cursor}"); Assert.Equal(HttpStatusCode.BadRequest, crossOwner.StatusCode);
        using var crossSize = await owner.GetAsync($"/api/photographers?pageSize=10&cursor={cursor}"); Assert.Equal(HttpStatusCode.BadRequest, crossSize.StatusCode);
        var empty = await stranger.GetFromJsonAsync<JsonElement>("/api/photographers"); Assert.Empty(empty.GetProperty("items").EnumerateArray()); Assert.Equal(0, empty.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("cursor=invalid")]
    public async Task Invalid_pagination_returns_a_validation_error(string query)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var response = await owner.GetAsync($"/api/photographers?{query}"); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
