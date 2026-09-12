using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.References;

// Given an owned reference, replacing its image preserves editorial metadata,
// boards and tags; stale/foreign requests do not change the saved image.
public sealed class ReplaceReferenceImageTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Replacement_preserves_metadata_boards_and_tags_and_is_receipted()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var upload = await ReferenceFixture.SubmitAsync(owner, new Dictionary<string, string> { ["title"] = "Keep this title", ["notes"] = "Keep these notes", ["sourceUrl"] = "https://source.example/work", ["attribution"] = "Casey Example" });
        var original = await upload.Content.ReadFromJsonAsync<JsonElement>(); var id = original.GetProperty("id").GetGuid();
        using var created = await owner.PostAsJsonAsync("/api/boards", new { name = "Window light" });
        var boardId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var membership = await owner.PutAsJsonAsync($"/api/references/{id}/boards", new { revision = 1, boardIds = new[] { boardId } }); membership.EnsureSuccessStatusCode();
        using var tags = await owner.PutAsJsonAsync($"/api/references/{id}/tags", new { revision = 2, tags = new[] { new { name = "soft light", category = "lighting" } } }); tags.EnsureSuccessStatusCode();
        using var image = NetVips.Image.Black(16, 12, bands: 3);
        var bytes = image.PngsaveBuffer(); var key = Guid.NewGuid().ToString();
        using var replace = await Replace(owner, id, 3, bytes, key);
        Assert.Equal(HttpStatusCode.OK, replace.StatusCode);
        using var replay = await Replace(owner, id, 3, bytes, key); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var result = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{id}");
        Assert.Equal(4, result.GetProperty("revision").GetInt64());
        Assert.Equal(16, result.GetProperty("width").GetInt32()); Assert.Equal(12, result.GetProperty("height").GetInt32());
        foreach (var field in new[] { "title", "notes", "sourceUrl", "attribution" }) Assert.Equal(original.GetProperty(field).GetString(), result.GetProperty(field).GetString());
        Assert.Equal(boardId, Assert.Single(result.GetProperty("boardIds").EnumerateArray()).GetGuid());
        Assert.Equal("soft light", Assert.Single(result.GetProperty("tags").EnumerateArray()).GetProperty("name").GetString());
        Assert.NotEqual(original.GetProperty("imageUrl").GetString(), result.GetProperty("imageUrl").GetString());
        using var fetched = await owner.GetAsync(result.GetProperty("imageUrl").GetString()); Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        using var decoded = NetVips.Image.NewFromBuffer(await fetched.Content.ReadAsByteArrayAsync()); Assert.Equal(16, decoded.Width);
    }

    [Fact]
    public async Task Foreign_stale_and_invalid_replacements_leave_saved_content_unchanged()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var upload = await ReferenceFixture.SubmitAsync(owner); var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var before = await owner.GetStringAsync($"/api/references/{id}");
        using var image = NetVips.Image.Black(16, 12, bands: 3); var bytes = image.PngsaveBuffer();
        using var foreign = await Replace(stranger, id, 1, bytes); Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var stale = await Replace(owner, id, 8, bytes); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var invalid = await Replace(owner, id, 1, [1, 2, 3]); Assert.Equal(HttpStatusCode.UnsupportedMediaType, invalid.StatusCode);
        Assert.Equal(before, await owner.GetStringAsync($"/api/references/{id}"));
    }

    private static async Task<HttpResponseMessage> Replace(HttpClient client, Guid id, long revision, byte[] bytes, string? key = null)
    {
        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes); part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(part, "image", "Replacement.png"); form.Add(new StringContent(revision.ToString()), "revision");
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/references/{id}/image") { Content = form };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString()); return await client.SendAsync(request);
    }
}
