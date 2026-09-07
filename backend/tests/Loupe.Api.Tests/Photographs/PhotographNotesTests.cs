// Given personal notes, when saved/revised/cleared, then they persist separately and reject stale or oversized edits.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class PhotographNotesTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task L2_004_1_4_Notes_persist_normalized_and_clear(bool maximum)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync();
        var photo = await PhotographFixture.UploadAsync(client);
        var url = $"/api/photographs/{photo.GetProperty("id").GetGuid()}";
        var text = maximum ? string.Concat(Enumerable.Repeat("📷", 10000)) : "  Step closer\r\nWatch the edges\rTry again  ";
        using var response = await client.PutAsJsonAsync(url + "/notes", new { revision = 1, notes = text });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var second = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var later = await second.CreateAuthenticatedClientAsync();
        var saved = await later.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal(maximum ? text : "Step closer\nWatch the edges\nTry again", saved.GetProperty("notes").GetString());
        Assert.Equal(2, saved.GetProperty("revision").GetInt64());
        using var cleared = await later.PutAsJsonAsync(url + "/notes", new { revision = 2, notes = " \r\n " });
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var empty = await client.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal(JsonValueKind.Null, empty.GetProperty("notes").ValueKind);
        Assert.Equal(3, empty.GetProperty("revision").GetInt64());
    }

    [Theory]
    [InlineData(0, 20, HttpStatusCode.BadRequest)]
    [InlineData(2, 10001, HttpStatusCode.BadRequest)]
    [InlineData(1, 20, HttpStatusCode.Conflict)]
    public async Task L2_004_4_L2_030_3_Rejected_notes_keep_saved_text(long revision, int length, HttpStatusCode expected)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync();
        var photo = await PhotographFixture.UploadAsync(client);
        var url = $"/api/photographs/{photo.GetProperty("id").GetGuid()}";
        using var baseline = await client.PutAsJsonAsync(url + "/notes", new { revision = 1, notes = "Keep this" });
        Assert.Equal(HttpStatusCode.OK, baseline.StatusCode);
        using var rejected = await client.PutAsJsonAsync(url + "/notes", new { revision, notes = new string('a', length) });
        Assert.Equal(expected, rejected.StatusCode);
        var saved = await client.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal("Keep this", saved.GetProperty("notes").GetString());
        Assert.Equal(2, saved.GetProperty("revision").GetInt64());
    }

    [Fact]
    public async Task L2_029_2_Another_owner_cannot_read_or_change_photograph_or_media()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync();
        using var stranger = await factory.CreateAuthenticatedClientAsync("owner-b");
        var photo = await PhotographFixture.UploadAsync(owner);
        foreach (var id in new[] { photo.GetProperty("id").GetGuid(), Guid.NewGuid() })
        {
            var url = $"/api/photographs/{id}";
            foreach (var suffix in new[] { "", "/image", "/preview" })
            {
                using var read = await stranger.GetAsync(url + suffix);
                Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
                var problem = await read.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal("item_unavailable", problem.GetProperty("code").GetString());
            }
            using var update = await stranger.PutAsJsonAsync(url + "/notes", new { revision = 1, notes = "Overwrite" });
            Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
            var error = await update.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("item_unavailable", error.GetProperty("code").GetString());
        }
    }
}
