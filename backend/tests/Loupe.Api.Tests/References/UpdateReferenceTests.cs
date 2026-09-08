using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.References;

// Given an owned reference, when manual metadata is revised, then all normalized
// fields persist together and invalid/stale/foreign edits preserve saved content.
public sealed class UpdateReferenceTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task L2_012_2_Metadata_persists_at_normalized_boundaries_and_optional_fields_clear(bool maximum)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync();
        using var uploaded = await ReferenceFixture.SubmitAsync(client);
        var original = await uploaded.Content.ReadFromJsonAsync<JsonElement>();
        var url = $"/api/references/{original.GetProperty("id").GetGuid()}";
        var title = maximum ? string.Concat(Enumerable.Repeat("📷", 200)) : "  Changed title  ";
        var attribution = maximum ? string.Concat(Enumerable.Repeat("📷", 200)) : "  Supplied photographer  ";
        var sourceUrl = maximum ? "https://example.test/" + new string('a', 2048 - "https://example.test/".Length) : " https://example.test/photo ";
        var notes = maximum ? string.Concat(Enumerable.Repeat("📷", 10000)) : "  Study edges\r\nKeep context\rTry again  ";
        using var response = await client.PutAsJsonAsync(url, new { revision = 1, title, sourceUrl, attribution, notes });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var second = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var later = await second.CreateAuthenticatedClientAsync();
        var saved = await later.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal(title.Trim(), saved.GetProperty("title").GetString());
        Assert.Equal(attribution.Trim(), saved.GetProperty("attribution").GetString());
        Assert.Equal(sourceUrl.Trim(), saved.GetProperty("sourceUrl").GetString());
        Assert.Equal(maximum ? notes : "Study edges\nKeep context\nTry again", saved.GetProperty("notes").GetString());
        Assert.Equal(2, saved.GetProperty("revision").GetInt64());
        foreach (var field in new[] { "id", "createdAt", "width", "height", "imageUrl", "previewUrl" })
            Assert.Equal(original.GetProperty(field).GetRawText(), saved.GetProperty(field).GetRawText());
        using var image = await later.GetAsync(saved.GetProperty("imageUrl").GetString());
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        using var cleared = await later.PutAsJsonAsync(url, new { revision = 2, title = "Kept title", sourceUrl = " ", attribution = "\r\n", notes = " " });
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var empty = await client.GetFromJsonAsync<JsonElement>(url);
        foreach (var field in new[] { "sourceUrl", "attribution", "notes" }) Assert.Equal(JsonValueKind.Null, empty.GetProperty(field).ValueKind);
        Assert.Equal(3, empty.GetProperty("revision").GetInt64());
        var page = await client.GetFromJsonAsync<JsonElement>("/api/references");
        Assert.Equal("Kept title", page.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == original.GetProperty("id").GetGuid()).GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("title", 201)]
    [InlineData("attribution", 201)]
    [InlineData("notes", 10001)]
    [InlineData("sourceUrl", 2049)]
    [InlineData("title", 0)]
    public async Task L2_009_4_L2_012_2_Invalid_metadata_does_not_partially_change_a_reference(string field, int length)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync();
        using var upload = await ReferenceFixture.SubmitAsync(client, new Dictionary<string, string> { ["notes"] = "Original note" });
        var original = await upload.Content.ReadFromJsonAsync<JsonElement>();
        var url = $"/api/references/{original.GetProperty("id").GetGuid()}";
        var input = new Dictionary<string, object> { ["revision"] = 1, ["title"] = "Changed", ["notes"] = "Changed notes", [field] = new string('a', length) };
        using var rejected = await client.PutAsJsonAsync(url, input);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var error = await rejected.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_request", error.GetProperty("code").GetString());
        Assert.True(error.GetProperty("errors").TryGetProperty(field, out _));
        var saved = await client.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal(original.GetRawText(), saved.GetRawText());
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("/relative")]
    [InlineData("https://user:password@example.test/photo")]
    [InlineData("https://example.test:8443/photo")]
    public async Task L2_012_2_Unsafe_sources_leave_existing_metadata_unchanged(string sourceUrl)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync();
        using var upload = await ReferenceFixture.SubmitAsync(client);
        var original = await upload.Content.ReadFromJsonAsync<JsonElement>();
        var url = $"/api/references/{original.GetProperty("id").GetGuid()}";
        using var rejected = await client.PutAsJsonAsync(url, new { revision = 1, title = "Changed", sourceUrl });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal(original.GetRawText(), (await client.GetFromJsonAsync<JsonElement>(url)).GetRawText());
    }

    [Fact]
    public async Task L2_030_3_Concurrent_edits_have_one_winner_and_stale_or_missing_revisions_cannot_overwrite_it()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync();
        using var upload = await ReferenceFixture.SubmitAsync(client);
        var original = await upload.Content.ReadFromJsonAsync<JsonElement>();
        var url = $"/api/references/{original.GetProperty("id").GetGuid()}";
        var responses = await Task.WhenAll(Enumerable.Range(1, 2).Select(index => client.PutAsJsonAsync(url, new { revision = 1, title = $"Winner {index}", notes = $"Note {index}" })));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            var winner = await responses.Single(response => response.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<JsonElement>();
            var conflict = await responses.Single(response => response.StatusCode == HttpStatusCode.Conflict).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("revision_conflict", conflict.GetProperty("code").GetString());
            using var stale = await client.PutAsJsonAsync(url, new { revision = 1, title = "Stale" });
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            using var missing = await client.PutAsJsonAsync(url, new { title = "Missing revision" });
            Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
            Assert.Equal(winner.GetRawText(), (await client.GetFromJsonAsync<JsonElement>(url)).GetRawText());
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task L2_029_2_Another_owner_or_anonymous_user_cannot_edit_a_reference()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync();
        using var stranger = await factory.CreateAuthenticatedClientAsync("stranger");
        using var anonymous = factory.CreateClient();
        using var upload = await ReferenceFixture.SubmitAsync(owner);
        var original = await upload.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var id in new[] { original.GetProperty("id").GetGuid(), Guid.NewGuid() })
        {
            using var response = await stranger.PutAsJsonAsync($"/api/references/{id}", new { revision = 1, title = "Overwrite" });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("item_unavailable", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            using var denied = await anonymous.PutAsJsonAsync($"/api/references/{id}", new { revision = 1, title = "Overwrite" });
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
        Assert.Equal(original.GetRawText(), (await owner.GetFromJsonAsync<JsonElement>($"/api/references/{original.GetProperty("id").GetGuid()}")).GetRawText());
    }
}
