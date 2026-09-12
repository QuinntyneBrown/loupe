using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.Photographers;

public sealed class CreateLinkedPhotographerTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Inline_creation_and_link_are_atomic_private_and_safe_to_retry()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var source = await Post(owner, "/api/references/links", new { title = "Window", sourceUrl = "https://reference.example/", attribution = "Original credit" });
        source.EnsureSuccessStatusCode();
        var reference = (await source.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reference").GetProperty("id").GetGuid();
        var route = $"/api/references/{reference}/photographer";
        var input = new { revision = 1, name = "Casey", portfolioUrl = "https://casey.example/" };
        using var foreign = await Post(stranger, route, input); Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(0, (await stranger.GetFromJsonAsync<JsonElement>("/api/photographers")).GetProperty("totalCount").GetInt32());
        using var stale = await Post(owner, route, new { revision = 2, name = "Casey", portfolioUrl = "https://casey.example/" });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(0, (await owner.GetFromJsonAsync<JsonElement>("/api/photographers")).GetProperty("totalCount").GetInt32());
        using var linked = await Post(owner, route, input, "inline-save"); Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        var result = await linked.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Casey", result.GetProperty("photographer").GetProperty("name").GetString());
        Assert.Equal("Original credit", result.GetProperty("attribution").GetString());
        Assert.Equal(2, result.GetProperty("revision").GetInt64());
        using var replay = await Post(owner, route, input, "inline-save"); replay.EnsureSuccessStatusCode();
        Assert.Equal(2, (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revision").GetInt64());
        Assert.Equal(1, (await owner.GetFromJsonAsync<JsonElement>("/api/photographers")).GetProperty("totalCount").GetInt32());
        using var changed = await Post(owner, route, new { revision = 2, name = "Other", portfolioUrl = "https://other.example/" }, "inline-save");
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
    }

    [Fact]
    public async Task Existing_normalized_portfolio_is_linked_without_overwriting_its_metadata()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var saved = await Post(owner, "/api/photographers", new { name = "Original", portfolioUrl = "https://casey.example/", notes = "Private notes" }); saved.EnsureSuccessStatusCode();
        var id = (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("photographer").GetProperty("id").GetGuid();
        using var source = await Post(owner, "/api/references/links", new { title = "Window", sourceUrl = "https://reference.example/" }); source.EnsureSuccessStatusCode();
        var reference = (await source.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reference").GetProperty("id").GetGuid();
        using var linked = await Post(owner, $"/api/references/{reference}/photographer", new { revision = 1, name = "Different", portfolioUrl = "HTTPS://Casey.Example:443/#about" });
        linked.EnsureSuccessStatusCode(); var result = await linked.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, result.GetProperty("photographer").GetProperty("id").GetGuid()); Assert.Equal("Original", result.GetProperty("attribution").GetString());
        Assert.Equal("Private notes", (await owner.GetFromJsonAsync<JsonElement>($"/api/photographers/{id}")).GetProperty("notes").GetString());
    }

    private static async Task<HttpResponseMessage> Post(HttpClient owner, string route, object input, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(input) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString()); return await owner.SendAsync(request);
    }
}
