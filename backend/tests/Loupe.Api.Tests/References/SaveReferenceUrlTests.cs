using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Loupe.Api.Tests.Persistence;

namespace Loupe.Api.Tests.References;

// Given a source URL, when saved, then a usable private link remains independent
// of fetching, and equivalent owned sources resolve to an existing reference.
public sealed class SaveReferenceUrlTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData("https://example.test:00443/photo", "https://example.test/photo")]
    [InlineData("http://example.test:00080/photo", "http://example.test/photo")]
    public async Task L2_010_4_All_standard_port_spellings_share_the_saved_reference(string source, string equivalent)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var first = await SubmitAsync(client, source);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var duplicate = await SubmitAsync(client, equivalent);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal((await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reference").GetProperty("id").GetGuid(),
            (await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reference").GetProperty("id").GetGuid());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task L2_010_4_URL_saving_waits_for_a_concurrent_upload_or_source_edit(bool editing)
    {
        var pause = new PausedCommit();
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot) { TransactionInterceptor = pause };
        var subject = Guid.NewGuid().ToString();
        using var client = await factory.CreateAuthenticatedClientAsync(subject);
        using var baseline = await SubmitAsync(client, "https://example.test/baseline");
        Assert.Equal(HttpStatusCode.Created, baseline.StatusCode);
        var id = (await baseline.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reference").GetProperty("id").GetGuid();
        pause.Arm();
        var mutation = editing
            ? client.PutAsJsonAsync($"/api/references/{id}", new { revision = 1, title = "Edited source", sourceUrl = "https://example.test/racing" })
            : ReferenceFixture.SubmitAsync(client, new Dictionary<string, string> { ["sourceUrl"] = "https://example.test/racing" });
        Task<HttpResponseMessage>? link = null;
        try
        {
            await pause.Reached.WaitAsync(TimeSpan.FromSeconds(15));
            link = SubmitAsync(client, "HTTPS://EXAMPLE.TEST:443/racing#source");
            await Task.WhenAny(link, Task.Delay(500));
            Assert.False(link.IsCompleted);
            pause.Resume();
            var saved = await mutation;
            Assert.Equal(editing ? HttpStatusCode.OK : HttpStatusCode.Created, saved.StatusCode);
            var reference = await saved.Content.ReadFromJsonAsync<JsonElement>();
            var duplicate = await link;
            Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
            Assert.Equal(reference.GetProperty("id").GetGuid(), (await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reference").GetProperty("id").GetGuid());
        }
        finally
        {
            pause.Resume();
            (await mutation).Dispose();
            if (link is not null) (await link).Dispose();
        }
    }

    [Fact]
    public async Task L2_010_4_L2_011_2_Save_a_link_with_original_source_and_manual_context_without_fetching()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        var subject = Guid.NewGuid().ToString();
        using var client = await factory.CreateAuthenticatedClientAsync(subject);
        var mediaBefore = Directory.Exists(database.MediaRoot) ? Directory.GetFiles(database.MediaRoot, "*", SearchOption.AllDirectories).Order().ToArray() : [];
        const string source = "HTTPS://Source.Example:443/photo?utm_source=study#original";
        using var response = await SubmitAsync(client, source, "  Light study  ", "  Supplied author  ", "  Keep context\r\nStudy edges  ");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(result.GetProperty("alreadySaved").GetBoolean());
        var reference = result.GetProperty("reference");
        var id = reference.GetProperty("id").GetGuid();
        Assert.Equal(source, reference.GetProperty("sourceUrl").GetString());
        Assert.Equal("Light study", reference.GetProperty("title").GetString());
        Assert.Equal("Supplied author", reference.GetProperty("attribution").GetString());
        Assert.Equal("Keep context\nStudy edges", reference.GetProperty("notes").GetString());
        foreach (var field in new[] { "imageUrl", "previewUrl", "width", "height" }) Assert.Equal(JsonValueKind.Null, reference.GetProperty(field).ValueKind);
        await using var second = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var later = await second.CreateAuthenticatedClientAsync(subject);
        Assert.Equal(reference.GetRawText(), (await later.GetFromJsonAsync<JsonElement>($"/api/references/{id}")).GetRawText());
        using var image = await later.GetAsync($"/api/references/{id}/image");
        Assert.Equal(HttpStatusCode.NotFound, image.StatusCode);
        Assert.Empty((await later.GetFromJsonAsync<JsonElement>("/api/photographs")).GetProperty("items").EnumerateArray());
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<LibraryDbContext>().BackgroundOperations.AnyAsync(operation => operation.ResourceId == id));
        var mediaAfter = Directory.Exists(database.MediaRoot) ? Directory.GetFiles(database.MediaRoot, "*", SearchOption.AllDirectories).Order().ToArray() : [];
        Assert.Equal(mediaBefore, mediaAfter);
    }

    [Fact]
    public async Task L2_010_4_Normalization_deduplicates_concurrent_saves_without_losing_path_or_tracking_query()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        var subject = Guid.NewGuid().ToString();
        using var client = await factory.CreateAuthenticatedClientAsync(subject);
        var responses = await Task.WhenAll(new[] { "HTTPS://Example.Test:443/Photo?utm_source=study#one", "https://example.test/Photo?utm_source=study#two" }.Select(source => SubmitAsync(client, source)));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            var results = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<JsonElement>()));
            Assert.Single(results, result => result.GetProperty("alreadySaved").GetBoolean());
            Assert.Equal(results[0].GetProperty("reference").GetProperty("id").GetGuid(), results[1].GetProperty("reference").GetProperty("id").GetGuid());
            foreach (var source in new[] { "https://example.test/photo?utm_source=study", "https://example.test/Photo?utm_source=other", "http://example.test/Photo?utm_source=study" })
            {
                using var distinct = await SubmitAsync(client, source);
                Assert.Equal(HttpStatusCode.Created, distinct.StatusCode);
            }
            var page = await client.GetFromJsonAsync<JsonElement>("/api/references");
            Assert.Equal(4, page.GetProperty("items").GetArrayLength());
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task L2_010_4_Existing_uploaded_references_are_found_without_overwriting_their_image_or_metadata()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        var subject = Guid.NewGuid().ToString();
        using var client = await factory.CreateAuthenticatedClientAsync(subject);
        using var uploaded = await ReferenceFixture.SubmitAsync(client, new Dictionary<string, string> { ["sourceUrl"] = "https://example.test/photo#original", ["title"] = "Original image" });
        var reference = await uploaded.Content.ReadFromJsonAsync<JsonElement>();
        using var duplicate = await SubmitAsync(client, "HTTPS://EXAMPLE.TEST:443/photo#later", "Do not overwrite", "New attribution", "New notes");
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        var result = await duplicate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.GetProperty("alreadySaved").GetBoolean());
        Assert.Equal(reference.GetRawText(), result.GetProperty("reference").GetRawText());
        using var anotherImage = await ReferenceFixture.SubmitAsync(client, new Dictionary<string, string> { ["sourceUrl"] = "https://example.test/photo" });
        Assert.Equal(HttpStatusCode.Created, anotherImage.StatusCode);
    }

    [Fact]
    public async Task L2_010_4_Source_edits_and_clearing_update_duplicate_lookup()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        var subject = Guid.NewGuid().ToString();
        using var client = await factory.CreateAuthenticatedClientAsync(subject);
        using var first = await SubmitAsync(client, "https://example.test/old");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var id = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reference").GetProperty("id").GetGuid();
        using var edit = await client.PutAsJsonAsync($"/api/references/{id}", new { revision = 1, title = "Edited", sourceUrl = "https://example.test/new" });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        using var duplicate = await SubmitAsync(client, "https://example.test/new");
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal(id, (await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reference").GetProperty("id").GetGuid());
        using var old = await SubmitAsync(client, "https://example.test/old");
        Assert.Equal(HttpStatusCode.Created, old.StatusCode);
        using var clear = await client.PutAsJsonAsync($"/api/references/{id}", new { revision = 2, title = "Edited", sourceUrl = "" });
        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        using var fresh = await SubmitAsync(client, "https://example.test/new");
        Assert.Equal(HttpStatusCode.Created, fresh.StatusCode);
    }

    [Fact]
    public async Task L2_010_4_Key_replay_and_owner_isolation_preserve_one_owned_link()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync("other-owner");
        var key = Guid.NewGuid().ToString();
        using var first = await SubmitAsync(owner, "https://example.test", key: key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var original = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reference");
        Assert.Equal("example.test", original.GetProperty("title").GetString());
        Assert.Equal(JsonValueKind.Null, original.GetProperty("attribution").ValueKind);
        using var replay = await SubmitAsync(owner, "https://example.test", key: key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True((await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("alreadySaved").GetBoolean());
        using var conflict = await SubmitAsync(owner, "https://example.test/different", key: key);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var root = await SubmitAsync(owner, "https://example.test/#root");
        Assert.Equal(HttpStatusCode.OK, root.StatusCode);
        using var separate = await SubmitAsync(stranger, "https://example.test", key: key);
        Assert.Equal(HttpStatusCode.Created, separate.StatusCode);
        var other = (await separate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reference");
        Assert.NotEqual(original.GetProperty("id").GetGuid(), other.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task L2_010_4_A_maximum_unicode_source_is_saved_and_deduplicated()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        var subject = Guid.NewGuid().ToString();
        using var client = await factory.CreateAuthenticatedClientAsync(subject);
        var source = "https://example.test/" + string.Concat(Enumerable.Repeat("📷", 2048 - "https://example.test/".Length));
        using var first = await SubmitAsync(client, source);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var duplicate = await SubmitAsync(client, source);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/relative")]
    [InlineData("https://user:password@example.test")]
    [InlineData("https://example.test:8443")]
    public async Task L2_010_L2_039_Invalid_sources_create_no_partial_reference(string source)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        var subject = Guid.NewGuid().ToString();
        using var client = await factory.CreateAuthenticatedClientAsync(subject);
        using var response = await SubmitAsync(client, source);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("sourceUrl", out _));
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/references")).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task L2_029_2_Anonymous_source_saving_is_denied()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = factory.CreateClient();
        using var response = await SubmitAsync(client, "https://example.test/photo");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, string sourceUrl,
        string? title = null, string? attribution = null, string? notes = null, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/references/links") { Content = JsonContent.Create(new { sourceUrl, title, attribution, notes }) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }
}
