// Given explicit Live configuration and an admitted image/brief snapshot, when
// the Responses boundary replies, then only valid critiques publish; Demo is offline.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Loupe.Api.Tests.Photographs;
using Loupe.Application.Critiques;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.Critiques;

public sealed class LiveCritiqueTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData("Demo", false)]
    [InlineData("Live", false)]
    [InlineData("Live", true)]
    public async Task L2_006_1_008_4_036_1_041_1_Live_uses_only_admitted_analysis_inputs_and_validated_structured_output(string mode, bool malformed)
    {
        var calls = 0;
        string? submitted = null;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        using var transport = new ControlledAiTransport(async (request, cancellationToken) =>
        {
            calls++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://api.openai.com/v1/responses", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("fixture-only-openai-key", request.Headers.Authorization.Parameter);
            submitted = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    status = "completed",
                    output = new object[] { new { type = "reasoning", summary = Array.Empty<object>() },
                        new { type = "message", role = "assistant", content = new[] { new { type = "output_text",
                            text = malformed ? "not-json" : JsonSerializer.Serialize(CritiqueResultFixture.Valid(), json) } } } }
                })
            };
        });
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot)
        {
            Settings = new Dictionary<string, string?> { ["Ai:Mode"] = mode, ["Ai:ApiKey"] = "fixture-only-openai-key" },
            AiTransport = transport
        };
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var brief = await client.PutAsJsonAsync($"/api/photographs/{id}/brief", new { revision = 1, intent = "Deliberate soft focus", requestedFeedback = "Keep the quiet mood" });
        brief.EnsureSuccessStatusCode();
        using var notes = await client.PutAsJsonAsync($"/api/photographs/{id}/notes", new { revision = 2, notes = "Private journal never sent to analysis" });
        notes.EnsureSuccessStatusCode();
        var preview = await client.GetByteArrayAsync($"/api/photographs/{id}/preview");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{id}/critique")
        { Content = JsonContent.Create(new { revision = 3 }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var admitted = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        using var edit = await client.PutAsJsonAsync($"/api/photographs/{id}/brief", new { revision = 3, intent = "Later private intent" });
        edit.EnsureSuccessStatusCode();
        async Task Run()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            Assert.True(await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCritiqueCommand()));
        }
        await Run();
        if (mode == "Demo") Assert.Equal(0, calls);
        else
        {
            Assert.Equal(1, calls);
            Assert.NotNull(submitted);
            Assert.DoesNotContain("Private journal", submitted);
            Assert.DoesNotContain("Later private intent", submitted);
            Assert.DoesNotContain("fixture-only-openai-key", submitted);
            using var payload = JsonDocument.Parse(submitted);
            var root = payload.RootElement;
            Assert.Equal("gpt-5.4-mini-2026-03-17", root.GetProperty("model").GetString());
            Assert.False(root.GetProperty("store").GetBoolean());
            var content = root.GetProperty("input")[0].GetProperty("content");
            var image = content.EnumerateArray().Single(item => item.GetProperty("type").GetString() == "input_image");
            Assert.Equal("data:image/jpeg;base64," + Convert.ToBase64String(preview), image.GetProperty("image_url").GetString());
            Assert.Equal("high", image.GetProperty("detail").GetString());
            var inputText = content.EnumerateArray().Single(item => item.GetProperty("type").GetString() == "input_text").GetProperty("text").GetString()!;
            Assert.Contains("Deliberate soft focus", inputText);
            Assert.Contains("Keep the quiet mood", inputText);
            Assert.DoesNotContain("Key", inputText);
            var format = root.GetProperty("text").GetProperty("format");
            Assert.Equal("json_schema", format.GetProperty("type").GetString());
            Assert.True(format.GetProperty("strict").GetBoolean());
            var schema = format.GetProperty("schema");
            Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
            Assert.Equal(15, schema.GetProperty("required").GetArrayLength());
            Assert.True(schema.GetProperty("properties").TryGetProperty("visualHierarchy", out _));
        }
        if (malformed)
        {
            var waiting = await client.GetFromJsonAsync<JsonElement>(admitted.Headers.Location);
            Assert.Equal("Queued", waiting.GetProperty("status").GetString());
            Assert.Equal("invalid_output", waiting.GetProperty("failureCode").GetString());
            factory.Clock.Advance(TimeSpan.FromSeconds(5));
            await Run();
        }
        var status = await client.GetFromJsonAsync<JsonElement>(admitted.Headers.Location);
        Assert.Equal(malformed ? "Failed" : "Succeeded", status.GetProperty("status").GetString());
        using var result = await client.GetAsync($"/api/photographs/{id}/critique");
        Assert.Equal(malformed ? HttpStatusCode.NoContent : HttpStatusCode.OK, result.StatusCode);
        if (!malformed)
        {
            var critique = await result.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(mode, critique.GetProperty("mode").GetString());
            Assert.Equal("Deliberate soft focus", critique.GetProperty("brief").GetProperty("intent").GetString());
        }
        var photo = await client.GetFromJsonAsync<JsonElement>($"/api/photographs/{id}");
        Assert.Equal("Private journal never sent to analysis", photo.GetProperty("notes").GetString());
        Assert.Equal("Later private intent", photo.GetProperty("brief").GetProperty("intent").GetString());
    }
}
