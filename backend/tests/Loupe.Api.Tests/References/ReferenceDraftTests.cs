using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.References;

// Given a preview draft, only final Save may add a reference. Drafts and their
// media are private; Cancel leaves the library untouched (mock Save reference).
public sealed class ReferenceDraftTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Uploaded_preview_is_private_and_cancel_never_saves_a_reference()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var response = await ReferenceFixture.SubmitAsync(owner, path: "/api/reference-drafts/images");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var draft = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = draft.GetProperty("id").GetGuid();
        using var preview = await owner.GetAsync(draft.GetProperty("previewUrl").GetString());
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var privatePreview = await stranger.GetAsync(draft.GetProperty("previewUrl").GetString());
        Assert.Equal(HttpStatusCode.NotFound, privatePreview.StatusCode);
        var library = await owner.GetFromJsonAsync<JsonElement>("/api/references");
        Assert.Empty(library.GetProperty("items").EnumerateArray());
        using var cancel = await owner.DeleteAsync($"/api/reference-drafts/{id}");
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        using var unavailable = await owner.GetAsync($"/api/reference-drafts/{id}");
        Assert.Equal(HttpStatusCode.NotFound, unavailable.StatusCode);
        var after = await owner.GetFromJsonAsync<JsonElement>("/api/references");
        Assert.Empty(after.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Final_save_atomically_persists_edited_metadata_image_and_boards_and_retries_once()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var boardResponse = await owner.PostAsJsonAsync("/api/boards", new { name = "Studies" });
        var board = (await boardResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var upload = await ReferenceFixture.SubmitAsync(owner, path: "/api/reference-drafts/images");
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var draft = await upload.Content.ReadFromJsonAsync<JsonElement>();
        var id = draft.GetProperty("id").GetGuid();
        var key = Guid.NewGuid().ToString();
        var payload = new { revision = 1, title = "Edited preview", sourceUrl = "https://source.example/photo", attribution = "Supplied name", notes = "My observation", boardIds = new[] { board } };
        JsonElement saved = default;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/reference-drafts/{id}/save") { Content = JsonContent.Create(payload) };
            request.Headers.Add("Idempotency-Key", key);
            using var response = await owner.SendAsync(request);
            Assert.True(response.IsSuccessStatusCode, factory.Failure.Exception?.ToString());
            var result = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reference");
            if (attempt == 1) Assert.Equal(saved.GetProperty("id").GetGuid(), result.GetProperty("id").GetGuid());
            saved = result;
        }
        Assert.Equal("Edited preview", saved.GetProperty("title").GetString());
        Assert.Equal("My observation", saved.GetProperty("notes").GetString());
        Assert.Equal(board, Assert.Single(saved.GetProperty("boardIds").EnumerateArray()).GetGuid());
        using var image = await owner.GetAsync(saved.GetProperty("imageUrl").GetString());
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        var library = await owner.GetFromJsonAsync<JsonElement>("/api/references");
        Assert.Single(library.GetProperty("items").EnumerateArray());
        var boards = await owner.GetFromJsonAsync<JsonElement>("/api/boards");
        Assert.Equal(1, boards[0].GetProperty("referenceCount").GetInt32());
    }

    [Fact]
    public async Task Foreign_board_or_stale_revision_cannot_partially_commit_a_draft()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var upload = await ReferenceFixture.SubmitAsync(owner, path: "/api/reference-drafts/images");
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        foreach (var revision in new[] { 1, 2 })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/reference-drafts/{id}/save") { Content = JsonContent.Create(new { revision, title = "Preview", boardIds = new[] { Guid.NewGuid() } }) };
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            using var response = await owner.SendAsync(request);
            Assert.Equal(revision == 1 ? HttpStatusCode.NotFound : HttpStatusCode.Conflict, response.StatusCode);
        }
        var library = await owner.GetFromJsonAsync<JsonElement>("/api/references");
        Assert.Empty(library.GetProperty("items").EnumerateArray());
        using var draft = await owner.GetAsync($"/api/reference-drafts/{id}");
        Assert.Equal(HttpStatusCode.OK, draft.StatusCode);
    }
}
