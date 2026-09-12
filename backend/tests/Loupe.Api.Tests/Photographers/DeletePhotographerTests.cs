using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.Photographers;

public sealed class DeletePhotographerTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Deleting_a_bookmark_unlinks_references_preserves_their_content_and_replays_the_deletion()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        var subject = Guid.NewGuid().ToString(); using var owner = await factory.CreateAuthenticatedClientAsync(subject);
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var saveRequest = new HttpRequestMessage(HttpMethod.Post, "/api/photographers") { Content = JsonContent.Create(new { name = "Casey", portfolioUrl = "https://casey.example/", tags = new[] { new { name = "portrait" } } }) };
        var key = Guid.NewGuid().ToString(); saveRequest.Headers.Add("Idempotency-Key", key);
        using var saved = await owner.SendAsync(saveRequest); saved.EnsureSuccessStatusCode();
        var id = (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("photographer").GetProperty("id").GetGuid();
        using var referenceRequest = new HttpRequestMessage(HttpMethod.Post, "/api/references/links") { Content = JsonContent.Create(new { title = "Window", sourceUrl = "https://reference.example/", notes = "Study these shadows" }) };
        referenceRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var savedReference = await owner.SendAsync(referenceRequest); savedReference.EnsureSuccessStatusCode();
        var referenceId = (await savedReference.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reference").GetProperty("id").GetGuid();
        using var link = await owner.PutAsJsonAsync($"/api/references/{referenceId}/photographer", new { photographerId = id, revision = 1 }); link.EnsureSuccessStatusCode();
        using var foreign = await stranger.DeleteAsync($"/api/photographers/{id}?revision=1"); Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var stale = await owner.DeleteAsync($"/api/photographers/{id}?revision=9"); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var deleted = await owner.DeleteAsync($"/api/photographers/{id}?revision=1"); Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        var deletion = await deleted.Content.ReadFromJsonAsync<JsonElement>();
        using var unavailable = await owner.GetAsync($"/api/photographers/{id}"); Assert.Equal(HttpStatusCode.NotFound, unavailable.StatusCode);
        var list = await owner.GetFromJsonAsync<JsonElement>("/api/photographers"); Assert.Equal(0, list.GetProperty("totalCount").GetInt32());
        var reference = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{referenceId}");
        Assert.Equal(JsonValueKind.Null, reference.GetProperty("photographer").ValueKind); Assert.Equal("Casey", reference.GetProperty("attribution").GetString());
        Assert.Equal("Study these shadows", reference.GetProperty("notes").GetString()); Assert.Equal("https://reference.example/", reference.GetProperty("sourceUrl").GetString()); Assert.Equal(3, reference.GetProperty("revision").GetInt64());
        using var repeat = await owner.DeleteAsync($"/api/photographers/{id}?revision=1"); repeat.EnsureSuccessStatusCode();
        Assert.Equal(deletion.GetProperty("id").GetGuid(), (await repeat.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        using var replayRequest = new HttpRequestMessage(HttpMethod.Post, "/api/photographers") { Content = JsonContent.Create(new { name = "Casey", portfolioUrl = "https://casey.example/", tags = new[] { new { name = "portrait" } } }) }; replayRequest.Headers.Add("Idempotency-Key", key);
        using var replay = await owner.SendAsync(replayRequest); Assert.Equal(HttpStatusCode.NotFound, replay.StatusCode);
        using var hiddenJournal = await stranger.GetAsync($"/api/deletions/{deletion.GetProperty("id").GetGuid()}"); Assert.Equal(HttpStatusCode.NotFound, hiddenJournal.StatusCode);
    }
}
