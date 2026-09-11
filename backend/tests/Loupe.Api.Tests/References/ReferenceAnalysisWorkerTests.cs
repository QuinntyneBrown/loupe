using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Application.ReferenceAnalysis;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.References;

public sealed class ReferenceAnalysisWorkerTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Valid_visual_output_is_private_unreviewed_and_does_not_overwrite_editorial_content()
    {
        using var transport = new ControlledSourceTransport((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { status = "completed", output = new[] { new { type = "message", content = new[] { new { type = "output_text", text = "{\"description\":\"Soft window light across a quiet room.\",\"tags\":[{\"name\":\"soft light\",\"category\":\"lighting\"}]}" } } } } })
        }));
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot)
        { AiTransport = transport, Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only" } };
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var upload = await ReferenceFixture.SubmitAsync(owner, new Dictionary<string, string> { ["notes"] = "My private notes" });
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var description = await owner.PutAsJsonAsync($"/api/references/{id}/description", new { revision = 1, text = "My description" }); description.EnsureSuccessStatusCode();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/references/{id}/analysis") { Content = JsonContent.Create(new { revision = 2 }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString()); using var admitted = await owner.SendAsync(request); admitted.EnsureSuccessStatusCode();
        await using var worker = factory.Services.CreateAsyncScope();
        Assert.True(await worker.ServiceProvider.GetRequiredService<ISender>().Send(new RunReferenceAnalysisCommand()));
        var operation = await owner.GetFromJsonAsync<JsonElement>(admitted.Headers.Location); Assert.Equal("Succeeded", operation.GetProperty("status").GetString());
        var suggestions = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{id}/suggestions");
        Assert.Equal("Soft window light across a quiet room.", suggestions.GetProperty("description").GetString());
        Assert.Equal("soft light", Assert.Single(suggestions.GetProperty("tags").EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal("Live", suggestions.GetProperty("mode").GetString());
        var reference = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{id}");
        Assert.Equal("My description", reference.GetProperty("description").GetString()); Assert.Equal("My private notes", reference.GetProperty("notes").GetString());
        Assert.Empty(reference.GetProperty("tags").EnumerateArray());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var hidden = await stranger.GetAsync($"/api/references/{id}/suggestions"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }
}
