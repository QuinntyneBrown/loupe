// Acceptance Test: L2-054.1/3. Region evidence must survive provider validation and API persistence.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Loupe.Api.Tests.Photographs;
using Loupe.Application.Critiques;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.Critiques;

public sealed class CritiqueRegionTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData(0.25, 0.6, 0.2, true)]
    [InlineData(0, 1, 1, true)]
    [InlineData(-0.1, 0.6, 0.2, false)]
    [InlineData(0.5, 1.1, 0.2, false)]
    [InlineData(0.5, 0.5, 0, false)]
    [InlineData(0.5, 0.5, 1.1, false)]
    public async Task L2_054_1_Provider_regions_are_validated_and_round_trip(double x, double y, double size, bool valid)
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        var result = JsonSerializer.SerializeToNode(CritiqueResultFixture.Valid(), json)!;
        result["strengths"]![0]!["evidence"]![0]!["region"] = new JsonObject { ["x"] = x, ["y"] = y, ["size"] = size };
        using var transport = new ControlledAiTransport((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { status = "completed", output = new[] { new { type = "message", content = new[] { new { type = "output_text", text = result.ToJsonString() } } } } })
        }));
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot)
        { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only-key" }, AiTransport = transport };
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{id}/critique") { Content = JsonContent.Create(new { revision = 1 }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var admitted = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCritiqueCommand()));
        var operation = await client.GetFromJsonAsync<JsonElement>(admitted.Headers.Location);
        Assert.Equal(valid ? "Succeeded" : "Queued", operation.GetProperty("status").GetString());
        using var saved = await client.GetAsync($"/api/photographs/{id}/critique");
        if (valid)
        {
            var content = await saved.Content.ReadFromJsonAsync<JsonElement>();
            var region = content.GetProperty("content").GetProperty("strengths")[0].GetProperty("evidence")[0].GetProperty("region");
            Assert.Equal(x, region.GetProperty("x").GetDouble());
            Assert.Equal(y, region.GetProperty("y").GetDouble());
            Assert.Equal(size, region.GetProperty("size").GetDouble());
        }
        else
        {
            Assert.Equal("invalid_output", operation.GetProperty("failureCode").GetString());
            Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        }
        var photo = await client.GetFromJsonAsync<JsonElement>($"/api/photographs/{id}");
        using var deleted = await client.DeleteAsync($"/api/photographs/{id}?revision={photo.GetProperty("revision").GetInt64()}");
        deleted.EnsureSuccessStatusCode();
    }
}
