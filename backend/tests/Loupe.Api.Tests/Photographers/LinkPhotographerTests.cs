using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.Photographers;

public sealed class LinkPhotographerTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData(null)]
    [InlineData("Original credit")]
    public async Task Linking_replacing_and_unlinking_preserves_textual_attribution(string? attribution)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var photographer = await Save(owner, "/api/photographers", new { name = "Casey", portfolioUrl = "https://casey.example/" }, "photographer");
        var other = await Save(owner, "/api/photographers", new { name = "Robin", portfolioUrl = "https://robin.example/" }, "photographer");
        var reference = await Save(owner, "/api/references/links", new { title = "Window light", sourceUrl = "https://reference.example/", attribution, notes = "Private note" }, "reference");
        using var linked = await owner.PutAsJsonAsync($"/api/references/{reference}/photographer", new { photographerId = photographer, revision = 1 });
        Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        var result = await linked.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Casey", result.GetProperty("photographer").GetProperty("name").GetString());
        Assert.Equal(attribution ?? "Casey", result.GetProperty("attribution").GetString());
        using var rename = await owner.PutAsJsonAsync($"/api/photographers/{photographer}", new { name = "Casey Renamed", portfolioUrl = "https://casey.example/", revision = 1 }); rename.EnsureSuccessStatusCode();
        var reread = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{reference}");
        Assert.Equal("Casey Renamed", reread.GetProperty("photographer").GetProperty("name").GetString());
        Assert.Equal(attribution ?? "Casey", reread.GetProperty("attribution").GetString());
        using var stale = await owner.PutAsJsonAsync($"/api/references/{reference}/photographer", new { photographerId = other, revision = 1 }); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var replaced = await owner.PutAsJsonAsync($"/api/references/{reference}/photographer", new { photographerId = other, revision = 2 }); replaced.EnsureSuccessStatusCode();
        using var unlinked = await owner.PutAsJsonAsync($"/api/references/{reference}/photographer", new { photographerId = (Guid?)null, revision = 3 }); unlinked.EnsureSuccessStatusCode();
        var final = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{reference}");
        Assert.Equal(JsonValueKind.Null, final.GetProperty("photographer").ValueKind);
        Assert.Equal(attribution ?? "Casey", final.GetProperty("attribution").GetString());
        Assert.Equal("Private note", final.GetProperty("notes").GetString()); Assert.Equal("https://reference.example/", final.GetProperty("sourceUrl").GetString());
    }

    [Fact]
    public async Task Foreign_reference_or_photographer_is_unavailable_without_changing_the_link()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var photographer = await Save(stranger, "/api/photographers", new { name = "Private", portfolioUrl = "https://private.example/" }, "photographer");
        var reference = await Save(owner, "/api/references/links", new { title = "Window", sourceUrl = "https://reference.example/" }, "reference");
        using var foreignPhotographer = await owner.PutAsJsonAsync($"/api/references/{reference}/photographer", new { photographerId = photographer, revision = 1 }); Assert.Equal(HttpStatusCode.NotFound, foreignPhotographer.StatusCode);
        using var foreignReference = await stranger.PutAsJsonAsync($"/api/references/{reference}/photographer", new { photographerId = photographer, revision = 1 }); Assert.Equal(HttpStatusCode.NotFound, foreignReference.StatusCode);
        var persisted = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{reference}"); Assert.Equal(1, persisted.GetProperty("revision").GetInt64());
    }

    private static async Task<Guid> Save(HttpClient owner, string route, object input, string property)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(input) }; request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await owner.SendAsync(request); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty(property).GetProperty("id").GetGuid();
    }
}
