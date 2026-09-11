using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Application.ReferenceAnalysis;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.References;

public sealed class ReferenceSuggestionReviewTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private ApiFactory Factory() => new(database.ConnectionString, database.MediaRoot)
    {
        AiTransport = new ControlledSourceTransport((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = JsonContent.Create(new { status = "completed", output = new[] { new { type = "message", content = new[] { new { type = "output_text", text = "{\"description\":\"A sunlit room.\",\"tags\":[{\"name\":\"soft light\",\"category\":\"lighting\"},{\"name\":\"quiet\",\"category\":\"mood\"}]}" } } } } }) })),
        Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only" }
    };

    // Given pending suggestions, accepting an edited description/tag persists only the reviewed value and its provenance.
    [Fact]
    public async Task Edited_acceptance_and_dismissal_persist_without_changing_notes()
    {
        var subject = Guid.NewGuid().ToString(); Guid id; Guid operation;
        await using (var factory = Factory())
        {
            using var owner = await factory.CreateAuthenticatedClientAsync(subject);
            (id, operation) = await GenerateAsync(factory, owner);
            using var description = await Review(owner, id, operation, 2, "description", "accept", value: "  My sunlit room.  ");
            description.EnsureSuccessStatusCode();
            var active = await description.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("My sunlit room.", active.GetProperty("description").GetString());
            Assert.Equal("edited-ai", active.GetProperty("descriptionProvenance").GetString());
            using var tag = await Review(owner, id, operation, 3, "tag", "accept", name: "soft light", value: "window light", category: "lighting");
            tag.EnsureSuccessStatusCode();
            using var dismiss = await Review(owner, id, operation, 4, "tag", "dismiss", name: "quiet");
            dismiss.EnsureSuccessStatusCode();
        }
        await using var restart = Factory(); using var later = await restart.CreateAuthenticatedClientAsync(subject);
        var reference = await later.GetFromJsonAsync<JsonElement>($"/api/references/{id}");
        Assert.Equal("Private notes", reference.GetProperty("notes").GetString());
        var accepted = Assert.Single(reference.GetProperty("tags").EnumerateArray());
        Assert.Equal("window light", accepted.GetProperty("name").GetString()); Assert.Equal("edited-ai", accepted.GetProperty("provenance").GetString());
        var saved = await later.GetFromJsonAsync<JsonElement>($"/api/references/{id}/suggestions");
        Assert.Equal("accepted", saved.GetProperty("descriptionState").GetString());
        Assert.Equal(new[] { "accepted", "dismissed" }, saved.GetProperty("tags").EnumerateArray().Select(tag => tag.GetProperty("state").GetString()));
    }

    [Fact]
    public async Task Acceptance_preserves_existing_spelling_and_rejects_new_tags_at_the_limit()
    {
        await using var factory = Factory(); using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var (id, operation) = await GenerateAsync(factory, owner);
        var tags = Enumerable.Range(0, 49).Select(i => new { name = $"Tag {i}", category = (string?)null }).Append(new { name = "SOFT LIGHT", category = (string?)"lighting" }).ToArray();
        using var manual = await owner.PutAsJsonAsync($"/api/references/{id}/tags", new { revision = 2, tags }); manual.EnsureSuccessStatusCode();
        using var duplicate = await Review(owner, id, operation, 3, "tag", "accept", name: "soft light"); duplicate.EnsureSuccessStatusCode();
        var reference = await duplicate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(50, reference.GetProperty("tags").GetArrayLength());
        Assert.Contains(reference.GetProperty("tags").EnumerateArray(), tag => tag.GetProperty("name").GetString() == "SOFT LIGHT" && tag.GetProperty("provenance").GetString() == "manual");
        using var overLimit = await Review(owner, id, operation, 4, "tag", "accept", name: "quiet"); Assert.Equal(HttpStatusCode.BadRequest, overLimit.StatusCode);
        Assert.Contains("50", await overLimit.Content.ReadAsStringAsync());
        var pending = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{id}/suggestions");
        Assert.Equal("pending", pending.GetProperty("tags")[1].GetProperty("state").GetString());
    }

    [Fact]
    public async Task Review_requires_owned_current_pending_suggestions_and_valid_values()
    {
        await using var factory = Factory(); using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var (id, operation) = await GenerateAsync(factory, owner);
        using var foreign = await Review(stranger, id, operation, 2, "description", "accept"); Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var stale = await Review(owner, id, operation, 1, "description", "accept"); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var wrongGeneration = await Review(owner, id, Guid.NewGuid(), 2, "description", "accept"); Assert.Equal(HttpStatusCode.Conflict, wrongGeneration.StatusCode);
        using var invalid = await Review(owner, id, operation, 2, "description", "accept", value: new string('x', 4001)); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var dismissed = await Review(owner, id, operation, 2, "description", "dismiss"); dismissed.EnsureSuccessStatusCode();
        using var repeated = await Review(owner, id, operation, 3, "description", "accept"); Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        var reference = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{id}"); Assert.Equal(JsonValueKind.Null, reference.GetProperty("description").ValueKind);
        var saved = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{id}/suggestions"); Assert.Equal("dismissed", saved.GetProperty("descriptionState").GetString());
    }

    private static Task<HttpResponseMessage> Review(HttpClient owner, Guid id, Guid operationId, long revision, string target, string decision, string? name = null, string? value = null, string? category = null) =>
        owner.PutAsJsonAsync($"/api/references/{id}/suggestions", new { operationId, revision, target, decision, name, value, category });

    private static async Task<(Guid Id, Guid Operation)> GenerateAsync(ApiFactory factory, HttpClient owner)
    {
        using var upload = await ReferenceFixture.SubmitAsync(owner, new Dictionary<string, string> { ["notes"] = "Private notes" }); upload.EnsureSuccessStatusCode();
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/references/{id}/analysis") { Content = JsonContent.Create(new { revision = 1 }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString()); using var admitted = await owner.SendAsync(request); admitted.EnsureSuccessStatusCode();
        var operation = (await admitted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await using var worker = factory.Services.CreateAsyncScope(); Assert.True(await worker.ServiceProvider.GetRequiredService<ISender>().Send(new RunReferenceAnalysisCommand()));
        return (id, operation);
    }
}
