using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using Loupe.Application.ReferenceImports;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.References;

public sealed class SavedSourceWorkerTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private ApiFactory Factory()
    {
        using var image = NetVips.Image.Black(8, 6, bands: 3); var bytes = image.PngsaveBuffer();
        return new(database.ConnectionString, database.MediaRoot)
        {
            Settings = new Dictionary<string, string?> { ["Imports:Mode"] = "Live", ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only" },
            SourceTransport = new ControlledSourceTransport((request, _) =>
            {
                if (request.RequestUri!.AbsolutePath == "/robots.txt") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
                if (request.RequestUri.AbsolutePath == "/image.png")
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
                    response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png"); return Task.FromResult(response);
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<title>Imported title</title><meta name='author' content='Source author'><meta property='og:image' content='/image.png'>", System.Text.Encoding.UTF8, "text/html") });
            })
        };
    }

    // Given a saved link, importing its unchanged source fills the missing image without replacing user-authored metadata.
    [Fact]
    public async Task Saved_link_import_publishes_image_and_private_source_provenance_while_preserving_metadata()
    {
        await using var factory = Factory(); using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var reference = (await Post(owner, "/api/references/links", new { sourceUrl = "https://source.example/work", title = "My title", attribution = "My attribution", notes = "My notes" })).GetProperty("reference");
        var id = reference.GetProperty("id").GetGuid(); var admitted = await Post(owner, $"/api/references/{id}/imports", new { revision = 1 });
        using var edit = await owner.PutAsJsonAsync($"/api/references/{id}/notes", new { revision = 1, text = "Later private notes" }); edit.EnsureSuccessStatusCode();
        Assert.True(await Run(factory));
        var saved = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{id}");
        Assert.Equal("My title", saved.GetProperty("title").GetString()); Assert.Equal("My attribution", saved.GetProperty("attribution").GetString()); Assert.Equal("Later private notes", saved.GetProperty("notes").GetString());
        using var image = await owner.GetAsync(saved.GetProperty("imageUrl").GetString()); image.EnsureSuccessStatusCode();
        var provenance = saved.GetProperty("sourceImport"); Assert.Equal("Imported title", provenance.GetProperty("title").GetString()); Assert.Equal("Source author", provenance.GetProperty("attribution").GetString());
        Assert.Equal("https://source.example/work", provenance.GetProperty("fetchedUrl").GetString()); Assert.Equal(factory.Clock.GetUtcNow(), provenance.GetProperty("retrievedAt").GetDateTimeOffset());
        var operation = await owner.GetFromJsonAsync<JsonElement>($"/api/operations/{admitted.GetProperty("id").GetGuid()}"); Assert.Equal("Succeeded", operation.GetProperty("status").GetString());
        Assert.Equal("Queued", (await owner.GetFromJsonAsync<JsonElement>($"/api/references/{id}/analysis")).GetProperty("status").GetString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString()); using var hidden = await stranger.GetAsync($"/api/references/{id}"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    [Fact]
    public async Task Changed_source_cancels_old_import_instead_of_attaching_its_image_or_metadata()
    {
        await using var factory = Factory(); using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var reference = (await Post(owner, "/api/references/links", new { sourceUrl = "https://source.example/work" })).GetProperty("reference");
        var id = reference.GetProperty("id").GetGuid(); var admitted = await Post(owner, $"/api/references/{id}/imports", new { revision = 1 });
        using var edit = await owner.PutAsJsonAsync($"/api/references/{id}", new { revision = 1, title = "New source", sourceUrl = "https://source.example/changed" }); edit.EnsureSuccessStatusCode();
        Assert.True(await Run(factory));
        var saved = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{id}"); Assert.Equal(JsonValueKind.Null, saved.GetProperty("imageUrl").ValueKind); Assert.Equal(JsonValueKind.Null, saved.GetProperty("sourceImport").ValueKind);
        var operation = await owner.GetFromJsonAsync<JsonElement>($"/api/operations/{admitted.GetProperty("id").GetGuid()}"); Assert.Equal("Canceled", operation.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Final_draft_save_keeps_source_provenance_after_the_draft_expires()
    {
        var subject = Guid.NewGuid().ToString();
        await using var factory = Factory(); using var owner = await factory.CreateAuthenticatedClientAsync(subject);
        var draft = await Post(owner, "/api/reference-drafts/links", new { sourceUrl = "https://source.example/work" }); var id = draft.GetProperty("id").GetGuid();
        Assert.True(await Run(factory));
        var saved = (await Post(owner, $"/api/reference-drafts/{id}/save", new { revision = 2, title = "My corrected title", sourceUrl = "https://source.example/work", boardIds = Array.Empty<Guid>() })).GetProperty("reference");
        Assert.True(saved.TryGetProperty("sourceImport", out var provenance)); Assert.Equal("Imported title", provenance.GetProperty("title").GetString());
        factory.Clock.Advance(TimeSpan.FromHours(25));
        using var signedInAgain = await factory.CreateAuthenticatedClientAsync(subject);
        var later = await signedInAgain.GetFromJsonAsync<JsonElement>($"/api/references/{saved.GetProperty("id").GetGuid()}");
        Assert.Equal(provenance.GetRawText(), later.GetProperty("sourceImport").GetRawText()); Assert.Equal("My corrected title", later.GetProperty("title").GetString());
    }

    private static async Task<JsonElement> Post(HttpClient owner, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) }; request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await owner.SendAsync(request); response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static async Task<bool> Run(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunReferenceImportCommand());
    }
}
