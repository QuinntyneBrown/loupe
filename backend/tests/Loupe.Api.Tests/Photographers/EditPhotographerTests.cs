using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.Photographers;

public sealed class EditPhotographerTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Owner_can_edit_a_bookmark_while_stale_and_foreign_writes_are_rejected()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = await Save(owner, "https://portfolio.example/original");
        var edit = new { name = "Revised name", portfolioUrl = "https://portfolio.example/revised", summary = "My revised summary", notes = "My notes", tags = new[] { new { name = "portrait", category = "genre" } }, revision = 1 };
        using var hidden = await stranger.PutAsJsonAsync($"/api/photographers/{id}", edit);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var updated = await owner.PutAsJsonAsync($"/api/photographers/{id}", edit);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var result = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Revised name", result.GetProperty("name").GetString());
        Assert.Equal(2, result.GetProperty("revision").GetInt64());
        Assert.Equal("manual", result.GetProperty("summaryProvenance").GetString());
        Assert.Equal("portrait", Assert.Single(result.GetProperty("tags").EnumerateArray()).GetProperty("name").GetString());
        using var stale = await owner.PutAsJsonAsync($"/api/photographers/{id}", edit);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var persisted = await owner.GetFromJsonAsync<JsonElement>($"/api/photographers/{id}");
        Assert.Equal("My notes", persisted.GetProperty("notes").GetString());
    }

    [Fact]
    public async Task Editing_to_an_existing_normalized_portfolio_preserves_both_bookmarks()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var first = await Save(owner, "https://portfolio.example/one");
        var second = await Save(owner, "https://portfolio.example/two");
        using var conflict = await owner.PutAsJsonAsync($"/api/photographers/{second}", new { name = "Changed", portfolioUrl = "HTTPS://PORTFOLIO.EXAMPLE:443/one#section", revision = 1 });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var persisted = await owner.GetFromJsonAsync<JsonElement>($"/api/photographers/{second}");
        Assert.Equal("https://portfolio.example/two", persisted.GetProperty("portfolioUrl").GetString());
        Assert.Equal(1, persisted.GetProperty("revision").GetInt64());
        using var existing = await owner.GetAsync($"/api/photographers/{first}"); existing.EnsureSuccessStatusCode();
    }

    private static async Task<Guid> Save(HttpClient owner, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/photographers") { Content = JsonContent.Create(new { name = "Casey", portfolioUrl = url }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await owner.SendAsync(request); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("photographer").GetProperty("id").GetGuid();
    }
}
