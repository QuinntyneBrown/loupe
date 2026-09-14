using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Locations;
using Loupe.Api.Tests.References;
using Loupe.Api.Tests.Scouting;
using Loupe.Infrastructure.Ai;
using Loupe.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loupe.Api.Tests.Search;

// Acceptance Test
// Traces to: L2-028, L2-062
// Description: Every acknowledged location change records one durable index intent
// and the detail reports Updating search until it is current; a requested scouting
// report is reported as Processing report while it is queued or running and a failed
// report leaves indexing to proceed with the prior text; without an embedding
// endpoint the status is not-configured; deleting the location cancels its intent.
public sealed class LocationIndexTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    // L2-028, criterion 2; L2-062, criterion 5: each change is acknowledged with one queued LocationIndex operation and the status "updating"; intents coalesce while queued.
    [Fact]
    public async Task L2_028_2_L2_062_5_Every_change_records_an_index_intent_and_reports_updating()
    {
        await using var factory = Factory(configured: true);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var created = await LocationFixture.CreateAsync(owner, new { name = "Kew Bridge foreshore" });
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("updating", created.GetProperty("indexStatus").GetString());
        var operationId = created.GetProperty("indexOperationId").GetGuid();
        var operation = await owner.GetFromJsonAsync<JsonElement>($"/api/operations/{operationId}");
        Assert.Equal("LocationIndex", operation.GetProperty("type").GetString());
        Assert.Equal("Queued", operation.GetProperty("status").GetString());
        Assert.Equal(id, operation.GetProperty("resourceId").GetGuid());

        var revision = created.GetProperty("revision").GetInt64();
        using var details = await owner.PutAsJsonAsync($"/api/locations/{id}", new { revision, name = "Kew Bridge foreshore", locality = "Brentford" });
        details.EnsureSuccessStatusCode();
        var afterDetails = await details.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("updating", afterDetails.GetProperty("indexStatus").GetString());
        Assert.Equal(operationId, afterDetails.GetProperty("indexOperationId").GetGuid());

        using var notes = await owner.PutAsJsonAsync($"/api/locations/{id}/notes", new { revision = afterDetails.GetProperty("revision").GetInt64(), text = "Parking on Kew Green." });
        notes.EnsureSuccessStatusCode();
        var afterNotes = await notes.Content.ReadFromJsonAsync<JsonElement>();
        using var tags = await owner.PutAsJsonAsync($"/api/locations/{id}/tags", new { revision = afterNotes.GetProperty("revision").GetInt64(), tags = new[] { new { name = "river" } } });
        tags.EnsureSuccessStatusCode();
        var afterTags = await tags.Content.ReadFromJsonAsync<JsonElement>();
        var afterImage = await LocationFixture.AddImageAsync(owner, id);
        using var removed = await owner.DeleteAsync($"/api/locations/{id}/images/{LocationFixture.ImageIds(afterImage)[0]}?revision={afterImage.GetProperty("revision").GetInt64()}");
        removed.EnsureSuccessStatusCode();
        var afterRemoval = await removed.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var result in new[] { afterNotes, afterTags, afterImage, afterRemoval })
        {
            Assert.Equal("updating", result.GetProperty("indexStatus").GetString());
            Assert.Equal(operationId, result.GetProperty("indexOperationId").GetGuid());
        }
        var detail = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}");
        Assert.Equal("updating", detail.GetProperty("indexStatus").GetString());
        Assert.Equal("Queued", (await owner.GetFromJsonAsync<JsonElement>($"/api/operations/{operationId}")).GetProperty("status").GetString());

        using var deletion = await owner.DeleteAsync($"/api/locations/{id}?revision={detail.GetProperty("revision").GetInt64()}");
        Assert.Equal(HttpStatusCode.OK, deletion.StatusCode);
        Assert.Equal("Canceled", (await owner.GetFromJsonAsync<JsonElement>($"/api/operations/{operationId}")).GetProperty("status").GetString());
    }

    // L2-062, criterion 4: while a requested report is queued or running the wait is labeled "processing-report"; a failed report leaves the location "updating".
    [Fact]
    public async Task L2_062_4_A_requested_report_is_processing_report_until_it_settles_and_a_failure_leaves_indexing_to_proceed()
    {
        using var transport = new ControlledSourceTransport((_, _) => Task.FromResult(ScoutingReportFixture.Refusal()));
        await using var factory = Factory(configured: true, transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await LocationFixture.CreateAsync(owner, new { name = "Kew Bridge foreshore" })).GetProperty("id").GetGuid();
        var location = await LocationFixture.AddImageAsync(owner, id);
        var indexOperation = location.GetProperty("indexOperationId").GetGuid();
        using var admitted = await ScoutingReportFixture.RequestAsync(owner, id, location.GetProperty("revision").GetInt64());
        Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        var queued = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}");
        Assert.Equal("Queued", queued.GetProperty("reportStatus").GetString());
        Assert.Equal("processing-report", queued.GetProperty("indexStatus").GetString());

        using var host = Host(factory);
        await host.StartAsync(default);
        try
        {
            var failed = await ScoutingReportFixture.WaitForAsync(owner, admitted.Headers.Location, item => item.GetProperty("status").GetString() == "Failed");
            Assert.Equal("unsupported_input", failed.GetProperty("failureCode").GetString());
        }
        finally { await host.StopAsync(default); }
        var detail = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}");
        Assert.Equal("Failed", detail.GetProperty("reportStatus").GetString());
        Assert.Equal("updating", detail.GetProperty("indexStatus").GetString());
        Assert.Equal(indexOperation, detail.GetProperty("indexOperationId").GetGuid());
    }

    // L2-062, criterion 4: without an embedding endpoint the status is "not-configured" and the location otherwise behaves the same.
    [Fact]
    public async Task L2_062_4_Without_an_embedding_endpoint_the_status_is_not_configured()
    {
        await using var factory = Factory(configured: false);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var created = await LocationFixture.CreateAsync(owner, new { name = "Kew Bridge foreshore", notes = "Parking on Kew Green." });
        Assert.Equal("not-configured", created.GetProperty("indexStatus").GetString());
        var id = created.GetProperty("id").GetGuid();
        var detail = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}");
        Assert.Equal("not-configured", detail.GetProperty("indexStatus").GetString());
        Assert.Equal("Kew Bridge foreshore", detail.GetProperty("name").GetString());
        Assert.Equal([id], (await owner.GetFromJsonAsync<JsonElement>("/api/locations/search?query=parking")).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray());
    }

    private ApiFactory Factory(bool configured, HttpMessageHandler? transport = null) => new(database.ConnectionString, database.MediaRoot)
    {
        AiTransport = transport,
        Settings = new Dictionary<string, string?>
        {
            ["Ai:Mode"] = transport is null ? null : "Live",
            ["Ai:ApiKey"] = transport is null ? null : "fixture-only",
            ["Embeddings:Endpoint"] = configured ? "http://ollama.test:11434" : null
        }
    };

    private static AnalysisWorker Host(ApiFactory factory) => new(factory.Services.GetRequiredService<IServiceScopeFactory>(),
        factory.Services.GetRequiredService<IOptions<AiOptions>>(), NullLogger<AnalysisWorker>.Instance);
}
