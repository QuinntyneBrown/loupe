using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.References;
using Xunit;

namespace Loupe.Api.Tests.Photographers;

// L2-020: bookmarks persist independently of fetching, are private, and deduplicate normalized portfolios.
public sealed class SavePhotographerTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Manual_bookmark_persists_after_restart_without_fetching_its_portfolio()
    {
        var subject = Guid.NewGuid().ToString(); Guid id;
        using var transport = new ControlledSourceTransport((_, _) => throw new InvalidOperationException("Saving must not fetch the portfolio."));
        await using (var factory = new ApiFactory(database.ConnectionString, database.MediaRoot) { SourceTransport = transport })
        {
            using var owner = await factory.CreateAuthenticatedClientAsync(subject);
            using var saved = await Save(owner, new { name = "  Casey Example  ", portfolioUrl = "https://portfolio.example/work", summary = "Quiet window-light portraits.", notes = "My private study notes", tags = new[] { new { name = " portrait ", category = "genre" } } });
            Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
            var result = (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("photographer"); id = result.GetProperty("id").GetGuid();
            Assert.Equal("Casey Example", result.GetProperty("name").GetString()); Assert.Equal("manual", result.GetProperty("summaryProvenance").GetString());
            Assert.Equal("portrait", Assert.Single(result.GetProperty("tags").EnumerateArray()).GetProperty("name").GetString());
            using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
            using var hidden = await stranger.GetAsync($"/api/photographers/{id}"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
            Assert.Empty(transport.Requests);
        }
        await using var restart = new ApiFactory(database.ConnectionString, database.MediaRoot); using var later = await restart.CreateAuthenticatedClientAsync(subject);
        var bookmark = await later.GetFromJsonAsync<JsonElement>($"/api/photographers/{id}"); Assert.Equal("My private study notes", bookmark.GetProperty("notes").GetString());
        Assert.Equal("Quiet window-light portraits.", bookmark.GetProperty("summary").GetString()); Assert.Equal(1, bookmark.GetProperty("revision").GetInt64());
    }

    [Fact]
    public async Task Concurrent_normalized_duplicates_and_keyed_retries_return_one_owned_bookmark()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        var subject = Guid.NewGuid().ToString(); using var first = await factory.CreateAuthenticatedClientAsync(subject); using var second = await factory.CreateAuthenticatedClientAsync(subject);
        var responses = await Task.WhenAll(Save(first, new { name = "Original", portfolioUrl = "HTTPS://Portfolio.Example:00443/work#one" }, "first"), Save(second, new { name = "Other name", portfolioUrl = "https://portfolio.example/work#two" }, "second"));
        Guid id;
        try
        {
            Assert.All(responses, response => Assert.True(response.IsSuccessStatusCode));
            var values = await Task.WhenAll(responses.Select(async response => await response.Content.ReadFromJsonAsync<JsonElement>()));
            Assert.Single(values.Select(value => value.GetProperty("photographer").GetProperty("id").GetGuid()).Distinct());
            Assert.Single(values, value => !value.GetProperty("alreadySaved").GetBoolean()); id = values[0].GetProperty("photographer").GetProperty("id").GetGuid();
        }
        finally { foreach (var response in responses) response.Dispose(); }
        using var replay = await Save(first, new { name = "Original", portfolioUrl = "HTTPS://Portfolio.Example:00443/work#one" }, "first"); replay.EnsureSuccessStatusCode();
        Assert.Equal(id, (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("photographer").GetProperty("id").GetGuid());
        using var otherOwner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString()); using var independent = await Save(otherOwner, new { name = "My bookmark", portfolioUrl = "https://portfolio.example/work" });
        Assert.Equal(HttpStatusCode.Created, independent.StatusCode); Assert.NotEqual(id, (await independent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("photographer").GetProperty("id").GetGuid());
    }

    [Theory]
    [InlineData("name", "")]
    [InlineData("portfolioUrl", "javascript:alert(1)")]
    [InlineData("portfolioUrl", "http://127.0.0.1/private")]
    [InlineData("portfolioUrl", "http://localhost/private")]
    [InlineData("portfolioUrl", "https://user:password@portfolio.example/")]
    [InlineData("summary", "overlength")]
    public async Task Invalid_bookmarks_return_field_errors_without_fetching(string field, string value)
    {
        using var transport = new ControlledSourceTransport((_, _) => throw new InvalidOperationException("Invalid input must not fetch."));
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot) { SourceTransport = transport }; using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var input = new Dictionary<string, string> { ["name"] = "Casey", ["portfolioUrl"] = "https://portfolio.example/" }; input[field] = value == "overlength" ? new string('x', 4001) : value;
        using var result = await Save(owner, input); Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.True((await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty(field, out _)); Assert.Empty(transport.Requests);
    }

    private static async Task<HttpResponseMessage> Save(HttpClient owner, object input, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/photographers") { Content = JsonContent.Create(input) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString()); return await owner.SendAsync(request);
    }
}
