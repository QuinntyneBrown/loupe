// Given a syntactically valid provider response that violates the requested
// object contract, when processed, then it is rejected before publication.
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Loupe.Api.Tests.Photographs;
using Loupe.Application.Critiques;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.Critiques;

public sealed class LiveCritiqueStrictnessTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData("missing-assessable")]
    [InlineData("missing-nullable")]
    [InlineData("missing-evidence-field")]
    [InlineData("duplicate-result")]
    [InlineData("duplicate-envelope")]
    [InlineData("unknown-field")]
    public async Task L2_006_6_034_3_Incomplete_or_ambiguous_JSON_cannot_publish(string defect)
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        var result = JsonSerializer.SerializeToNode(CritiqueResultFixture.Valid(), json)!;
        switch (defect)
        {
            case "missing-assessable": result["focus"]!.AsObject().Remove("assessable"); break;
            case "missing-nullable": result["exposure"]!.AsObject().Remove("uncertaintyReason"); break;
            case "missing-evidence-field": result["strengths"]![0]!["evidence"]![0]!.AsObject().Remove("exifField"); break;
            case "unknown-field": result["inventedScore"] = 99; break;
        }
        var text = result.ToJsonString();
        if (defect == "duplicate-result") text = "{\"strengths\":null," + text[1..];
        var body = JsonSerializer.Serialize(new
        {
            status = "completed",
            output = new[] {
            new { type = "message", content = new[] { new { type = "output_text", text } } } }
        });
        if (defect == "duplicate-envelope") body = "{\"status\":\"incomplete\"," + body[1..];
        var calls = 0;
        using var transport = new ControlledAiTransport((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        });
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot)
        { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only-openai-key" }, AiTransport = transport };
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{id}/critique")
            { Content = JsonContent.Create(new { revision = 1 }) };
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            using var admitted = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
            await using var scope = factory.Services.CreateAsyncScope();
            Assert.True(await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCritiqueCommand()));
            Assert.Equal(1, calls);
            var operation = await client.GetFromJsonAsync<JsonElement>(admitted.Headers.Location);
            Assert.Equal("Queued", operation.GetProperty("status").GetString());
            Assert.Equal("invalid_output", operation.GetProperty("failureCode").GetString());
            using var critique = await client.GetAsync($"/api/photographs/{id}/critique");
            Assert.Equal(HttpStatusCode.NoContent, critique.StatusCode);
        }
        finally
        {
            var photo = await client.GetFromJsonAsync<JsonElement>($"/api/photographs/{id}");
            using var deleted = await client.DeleteAsync($"/api/photographs/{id}?revision={photo.GetProperty("revision").GetInt64()}");
            deleted.EnsureSuccessStatusCode();
        }
    }
}
