using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Locations;
using Loupe.Api.Tests.References;
using Loupe.Api.Tests.Scouting;
using Loupe.Infrastructure.Ai;
using Loupe.Infrastructure.Persistence;
using Loupe.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loupe.Api.Tests.Search;

// Acceptance Test
// Traces to: L2-028, L2-062, L2-041
// Description: The search index worker embeds a location's document through the
// local embedding endpoint and publishes its vector only while the location still
// stands at the revision it read, so the detail reports current after one pass, an
// edit acknowledged mid-flight leaves the stale vector unpublished, a failed
// embedding reports failed with a retry through the shared operations route,
// deletion removes the vector, and an unconfigured endpoint leaves the worker idle.
public sealed class LocationIndexWorkerTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    // L2-028, criterion 1; L2-062, criterion 4; L2-041: one worker pass makes the saved location current, the document carries every descriptive field and never a street address line.
    [Fact]
    public async Task L2_028_1_L2_062_4_A_saved_location_becomes_current_after_one_worker_pass_from_a_document_without_its_address()
    {
        using var transport = new ControlledEmbeddingTransport((_, _) => Task.FromResult(ControlledEmbeddingTransport.Vector(1)));
        var marker = Marker();
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var created = await LocationFixture.CreateAsync(owner, new
        {
            name = "Kew Bridge foreshore",
            addressLine1 = "12 Thames Path",
            addressLine2 = "Under the arches",
            locality = "Brentford",
            region = "Greater London",
            postalCode = "TW8 0EF",
            country = "United Kingdom",
            setting = "Outdoor",
            scoutingBrief = "Couples at low tide.",
            notes = "Parking on Kew Green. " + marker,
            tags = new[] { new { name = "riverside", category = (string?)"subject" }, new { name = "arches", category = (string?)null } }
        });
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("updating", created.GetProperty("indexStatus").GetString());
        using var host = Host(factory);
        await host.StartAsync(default);
        JsonElement detail;
        try { detail = await WaitForStatusAsync(owner, id, "current"); }
        finally { await host.StopAsync(default); }
        Assert.Equal("current", detail.GetProperty("indexStatus").GetString());
        var operation = await owner.GetFromJsonAsync<JsonElement>($"/api/operations/{detail.GetProperty("indexOperationId").GetGuid()}");
        Assert.Equal("Succeeded", operation.GetProperty("status").GetString());

        var (uri, body) = Assert.Single(transport.RequestsFor(marker));
        Assert.Equal("http://ollama.test:11434/api/embed", uri.ToString());
        var request = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal("bge-m3", request.GetProperty("model").GetString());
        var document = request.GetProperty("input").GetString()!;
        foreach (var expected in new[] { "Kew Bridge foreshore", "Brentford", "Greater London", "United Kingdom", "Outdoor", "riverside", "arches", "Couples at low tide.", "Parking on Kew Green." })
            Assert.Contains(expected, document);
        foreach (var excluded in new[] { "Thames Path", "Under the arches", "TW8" })
            Assert.DoesNotContain(excluded, document);
        Assert.Equal(detail.GetProperty("revision").GetInt64(), await VectorRevisionAsync(factory, id));
    }

    // L2-062, criterion 4: a report requested with the save is Processing report until it succeeds, then the document is re-embedded with the report text.
    [Fact]
    public async Task L2_062_4_A_published_report_re_embeds_the_document_with_its_text()
    {
        using var embeddings = new ControlledEmbeddingTransport((_, _) => Task.FromResult(ControlledEmbeddingTransport.Vector(2)));
        var report = ScoutingReportFixture.Valid(1);
        report["cautions"]![0]!["caution"] = "Slippery stones near the waterline.";
        using var ai = new ControlledSourceTransport((_, _) => Task.FromResult(ScoutingReportFixture.Output(report)));
        await using var factory = Factory(embeddings, ai);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var marker = Marker();
        var id = (await LocationFixture.CreateAsync(owner, new { name = "Kew Bridge foreshore", notes = marker })).GetProperty("id").GetGuid();
        var location = await LocationFixture.AddImageAsync(owner, id);
        using var admitted = await ScoutingReportFixture.RequestAsync(owner, id, location.GetProperty("revision").GetInt64());
        Assert.Equal("processing-report", (await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}")).GetProperty("indexStatus").GetString());
        using var index = Host(factory);
        using var analysis = AnalysisHost(factory);
        await index.StartAsync(default);
        await analysis.StartAsync(default);
        JsonElement detail;
        try
        {
            await ScoutingReportFixture.WaitForAsync(owner, admitted.Headers.Location, item => item.GetProperty("status").GetString() == "Succeeded");
            detail = await WaitForStatusAsync(owner, id, "current", requireReport: true);
        }
        finally
        {
            await analysis.StopAsync(default);
            await index.StopAsync(default);
        }
        Assert.Equal("Ready", detail.GetProperty("reportStatus").GetString());
        Assert.Equal("current", detail.GetProperty("indexStatus").GetString());
        var last = JsonSerializer.Deserialize<JsonElement>(embeddings.RequestsFor(marker).Last().Body).GetProperty("input").GetString()!;
        Assert.Contains("Slippery stones near the waterline.", last);
        Assert.Contains("Golden hour", last);
        Assert.Equal(detail.GetProperty("revision").GetInt64(), await VectorRevisionAsync(factory, id));
    }

    // L2-028, criterion 2; L2-062, criterion 5: an edit acknowledged after the worker read the document supersedes that run, so its vector is never published and the replacement is built from the edit.
    [Fact]
    public async Task L2_062_5_An_edit_acknowledged_mid_flight_leaves_the_stale_vector_unpublished_and_reindexes_the_edit()
    {
        var gate = new TaskCompletionSource();
        var marker = Marker();
        var calls = 0;
        using var transport = new ControlledEmbeddingTransport(async (body, token) =>
        {
            if (body.Contains(marker) && Interlocked.Increment(ref calls) == 1) await gate.Task.WaitAsync(token);
            return ControlledEmbeddingTransport.Vector(3);
        });
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var created = await LocationFixture.CreateAsync(owner, new { name = "Kew Bridge foreshore " + marker, notes = "First notes." });
        var id = created.GetProperty("id").GetGuid();
        var first = created.GetProperty("indexOperationId").GetGuid();
        using var host = Host(factory);
        await host.StartAsync(default);
        JsonElement detail;
        try
        {
            await WaitUntilAsync(() => transport.RequestsFor(marker).Count == 1);
            using var edit = await owner.PutAsJsonAsync($"/api/locations/{id}/notes", new { revision = created.GetProperty("revision").GetInt64(), text = "Edited notes." });
            edit.EnsureSuccessStatusCode();
            var edited = await edit.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("updating", edited.GetProperty("indexStatus").GetString());
            var second = edited.GetProperty("indexOperationId").GetGuid();
            Assert.NotEqual(first, second);
            Assert.Equal("Canceled", (await owner.GetFromJsonAsync<JsonElement>($"/api/operations/{first}")).GetProperty("status").GetString());
            gate.SetResult();
            detail = await WaitForStatusAsync(owner, id, "current");
            Assert.Equal(second, detail.GetProperty("indexOperationId").GetGuid());
        }
        finally { await host.StopAsync(default); }
        var requests = transport.RequestsFor(marker);
        Assert.Equal(2, requests.Count);
        Assert.Contains("First notes.", requests[0].Body);
        Assert.Contains("Edited notes.", requests[1].Body);
        Assert.Equal(detail.GetProperty("revision").GetInt64(), await VectorRevisionAsync(factory, id));
    }

    // L2-028, criterion 4; L2-062, criterion 6: a failed embedding reports failed while keyword search and editing continue, and the shared retry route re-queues it.
    [Fact]
    public async Task L2_028_4_L2_062_6_A_failed_embedding_reports_failed_and_retries_through_the_operations_route()
    {
        var failing = true;
        using var transport = new ControlledEmbeddingTransport((_, _) => Task.FromResult(failing
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : ControlledEmbeddingTransport.Vector(4)));
        var marker = Marker();
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var created = await LocationFixture.CreateAsync(owner, new { name = "Kew Bridge foreshore", notes = "Parking on Kew Green. " + marker });
        var id = created.GetProperty("id").GetGuid();
        var operationId = created.GetProperty("indexOperationId").GetGuid();
        using var host = Host(factory);
        await host.StartAsync(default);
        JsonElement detail;
        try
        {
            await ScoutingReportFixture.WaitForAsync(owner, new Uri($"/api/operations/{operationId}", UriKind.Relative), item => item.GetProperty("failureCode").GetString() == "provider_unavailable");
            factory.Clock.Advance(TimeSpan.FromSeconds(5));
            await Task.Delay(1500);
            factory.Clock.Advance(TimeSpan.FromSeconds(30));
            var failed = await ScoutingReportFixture.WaitForAsync(owner, new Uri($"/api/operations/{operationId}", UriKind.Relative), item => item.GetProperty("status").GetString() == "Failed");
            Assert.Equal("Failed", failed.GetProperty("status").GetString());
            Assert.Equal(3, transport.RequestsFor(marker).Count);
            detail = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}");
            Assert.Equal("failed", detail.GetProperty("indexStatus").GetString());
            Assert.Equal([id], (await owner.GetFromJsonAsync<JsonElement>("/api/locations/search?query=parking")).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray());
            Assert.Null(await VectorRevisionAsync(factory, id));

            failing = false;
            using var stale = await Retry(owner, operationId, detail.GetProperty("revision").GetInt64() + 1);
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            using var retried = await Retry(owner, operationId, detail.GetProperty("revision").GetInt64());
            Assert.Equal(HttpStatusCode.Accepted, retried.StatusCode);
            var replacement = await retried.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("LocationIndex", replacement.GetProperty("type").GetString());
            Assert.NotEqual(operationId, replacement.GetProperty("id").GetGuid());
            detail = await WaitForStatusAsync(owner, id, "current");
            Assert.Equal(replacement.GetProperty("id").GetGuid(), detail.GetProperty("indexOperationId").GetGuid());
        }
        finally { await host.StopAsync(default); }
        Assert.Equal(detail.GetProperty("revision").GetInt64(), await VectorRevisionAsync(factory, id));
    }

    // L2-028, criterion 5; L2-062, criterion 7: deleting the location removes its vector with the record.
    [Fact]
    public async Task L2_028_5_Deleting_the_location_removes_its_vector()
    {
        using var transport = new ControlledEmbeddingTransport((_, _) => Task.FromResult(ControlledEmbeddingTransport.Vector(5)));
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await LocationFixture.CreateAsync(owner, new { name = "Kew Bridge foreshore" })).GetProperty("id").GetGuid();
        using var host = Host(factory);
        await host.StartAsync(default);
        JsonElement detail;
        try { detail = await WaitForStatusAsync(owner, id, "current"); }
        finally { await host.StopAsync(default); }
        Assert.NotNull(await VectorRevisionAsync(factory, id));
        using var deletion = await owner.DeleteAsync($"/api/locations/{id}?revision={detail.GetProperty("revision").GetInt64()}");
        Assert.Equal(HttpStatusCode.OK, deletion.StatusCode);
        Assert.Null(await VectorRevisionAsync(factory, id));
    }

    // L2-062, criterion 4: without an embedding endpoint the worker stays idle, nothing is sent, and the intent waits.
    [Fact]
    public async Task L2_062_4_Without_an_embedding_endpoint_the_worker_stays_idle()
    {
        using var transport = new ControlledEmbeddingTransport((_, _) => Task.FromResult(ControlledEmbeddingTransport.Vector(6)));
        await using var factory = Factory(transport, configured: false);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var created = await LocationFixture.CreateAsync(owner, new { name = "Kew Bridge foreshore" });
        var id = created.GetProperty("id").GetGuid();
        using var host = Host(factory);
        await host.StartAsync(default);
        try { await Task.Delay(1500); }
        finally { await host.StopAsync(default); }
        Assert.Empty(transport.Requests);
        Assert.Equal("not-configured", (await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}")).GetProperty("indexStatus").GetString());
        Assert.Equal("Queued", (await owner.GetFromJsonAsync<JsonElement>($"/api/operations/{created.GetProperty("indexOperationId").GetGuid()}")).GetProperty("status").GetString());
    }

    private static string Marker() => "marker-" + Guid.NewGuid().ToString("N");

    private static async Task<JsonElement> WaitForStatusAsync(HttpClient client, Guid id, string status, bool requireReport = false)
    {
        JsonElement detail = default;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            detail = await client.GetFromJsonAsync<JsonElement>($"/api/locations/{id}");
            if (detail.GetProperty("indexStatus").GetString() == status && (!requireReport || detail.GetProperty("report").ValueKind == JsonValueKind.Object)) return detail;
            await Task.Delay(100);
        }
        return detail;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++) await Task.Delay(100);
        Assert.True(condition());
    }

    private static async Task<long?> VectorRevisionAsync(ApiFactory factory, Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var revisions = await context.Database.SqlQuery<long>($"SELECT \"SourceRevision\" AS \"Value\" FROM search_vectors WHERE \"ItemType\" = 'location' AND \"ItemId\" = {id}").ToListAsync();
        return revisions.Count == 0 ? null : revisions.Single();
    }

    private static async Task<HttpResponseMessage> Retry(HttpClient client, Guid operationId, long revision)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/operations/{operationId}/retry") { Content = JsonContent.Create(new { revision }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    private ApiFactory Factory(HttpMessageHandler embeddings, HttpMessageHandler? ai = null, bool configured = true) => new(database.ConnectionString, database.MediaRoot)
    {
        EmbeddingTransport = embeddings,
        AiTransport = ai,
        Settings = new Dictionary<string, string?>
        {
            ["Ai:Mode"] = ai is null ? null : "Live",
            ["Ai:ApiKey"] = ai is null ? null : "fixture-only",
            ["Embeddings:Endpoint"] = configured ? "http://ollama.test:11434" : null
        }
    };

    private static SearchIndexWorker Host(ApiFactory factory) =>
        new(factory.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<SearchIndexWorker>.Instance);

    private static AnalysisWorker AnalysisHost(ApiFactory factory) => new(factory.Services.GetRequiredService<IServiceScopeFactory>(),
        factory.Services.GetRequiredService<IOptions<AiOptions>>(), NullLogger<AnalysisWorker>.Instance);
}
