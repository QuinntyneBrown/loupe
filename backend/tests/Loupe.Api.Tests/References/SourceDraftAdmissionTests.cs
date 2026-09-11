using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.References;

// Given a source URL, Import admits a private temporary job; Cancel cancels that
// work and only an explicit final Save may produce a library reference.
public sealed class SourceDraftAdmissionTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private ApiFactory Factory(bool live = true) => new(database.ConnectionString, database.MediaRoot)
    { Settings = new Dictionary<string, string?> { ["Imports:Mode"] = live ? "Live" : null } };

    private static async Task<JsonElement> Start(HttpClient client, string source)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/reference-drafts/links") { Content = JsonContent.Create(new { sourceUrl = source }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Import_stays_out_of_the_library_and_cancel_terminates_the_private_job()
    {
        await using var factory = Factory();
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var draft = await Start(owner, "https://source.example/photograph");
        var id = draft.GetProperty("id").GetGuid();
        var operation = draft.GetProperty("import");
        Assert.Equal("Queued", operation.GetProperty("status").GetString());
        using var privateDraft = await stranger.GetAsync($"/api/reference-drafts/{id}");
        Assert.Equal(HttpStatusCode.NotFound, privateDraft.StatusCode);
        var library = await owner.GetFromJsonAsync<JsonElement>("/api/references");
        Assert.Empty(library.GetProperty("items").EnumerateArray());
        using var cancel = await owner.DeleteAsync($"/api/reference-drafts/{id}");
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        var canceled = await owner.GetFromJsonAsync<JsonElement>($"/api/operations/{operation.GetProperty("id").GetGuid()}");
        Assert.Equal("Canceled", canceled.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Unconfigured_import_keeps_a_manual_fallback_draft_that_can_be_saved_explicitly()
    {
        await using var factory = Factory(false);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var draft = await Start(owner, "https://source.example/fallback");
        Assert.Equal("integration_not_configured", draft.GetProperty("failureCode").GetString());
        Assert.Equal(JsonValueKind.Null, draft.GetProperty("import").ValueKind);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/reference-drafts/{draft.GetProperty("id").GetGuid()}/save")
        { Content = JsonContent.Create(new { revision = 1, title = "Keep the source", sourceUrl = draft.GetProperty("sourceUrl").GetString(), notes = "Find the image later", boardIds = Array.Empty<Guid>() }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await owner.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, factory.Failure.Exception?.ToString());
        var reference = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reference");
        Assert.Equal("Find the image later", reference.GetProperty("notes").GetString());
        Assert.Equal(JsonValueKind.Null, reference.GetProperty("imageUrl").ValueKind);
    }

    [Fact]
    public async Task Already_saved_sources_return_the_owned_reference_without_an_import_job()
    {
        await using var factory = Factory();
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var saved = await ReferenceFixture.SubmitAsync(owner, new Dictionary<string, string> { ["sourceUrl"] = "https://source.example/photo#first" });
        var reference = await saved.Content.ReadFromJsonAsync<JsonElement>();
        var duplicate = await Start(owner, "https://SOURCE.example/photo#second");
        Assert.Equal(reference.GetProperty("id").GetGuid(), duplicate.GetProperty("committedReferenceId").GetGuid());
        Assert.Equal(JsonValueKind.Null, duplicate.GetProperty("import").ValueKind);
        var privateMatch = await Start(stranger, "https://source.example/photo");
        Assert.Equal(JsonValueKind.Null, privateMatch.GetProperty("committedReferenceId").ValueKind);
        Assert.Equal("Queued", privateMatch.GetProperty("import").GetProperty("status").GetString());
    }
}
