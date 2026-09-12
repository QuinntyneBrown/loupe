using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Loupe.Api.Tests.Locations;
using Loupe.Api.Tests.References;
using Loupe.Infrastructure.Ai;
using Loupe.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loupe.Api.Tests.Scouting;

// Acceptance Test
// Traces to: L2-058, L2-059, L2-060, L2-034
// Description: The analysis host generates a scouting report from the location's
// images and brief through the controlled provider, validates every section, and
// publishes only a complete report; invalid shapes and provider failures follow the
// shared retry classes and leave the current report unchanged.
public sealed class ScoutingWorkerTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    // L2-058, criterion 1; L2-059, criterion 3; L2-060, criterion 8: a valid result is saved in order with cited images and provenance, from a request holding only images, brief, and EXIF.
    [Fact]
    public async Task L2_058_1_L2_059_3_A_valid_result_is_saved_in_section_order_with_cited_images_and_provenance()
    {
        var bodies = new List<string>();
        using var transport = new ControlledSourceTransport(async (request, token) =>
        {
            bodies.Add(await request.Content!.ReadAsStringAsync(token));
            return ScoutingReportFixture.Output(ScoutingReportFixture.Valid(1, 3));
        });
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var created = await LocationFixture.CreateAsync(owner, new
        {
            name = "Kew Bridge foreshore",
            addressLine1 = "SECRET-ADDRESS Thames Path",
            locality = "SECRET-LOCALITY",
            coordinates = new { latitude = "51.487213", longitude = "-0.287604" },
            scoutingBrief = "Couple sessions at low tide, two people.",
            notes = "SECRET-NOTES parking on Kew Green",
            tags = new[] { new { name = "SECRET-TAG", category = "subject" } }
        });
        var id = created.GetProperty("id").GetGuid();
        JsonElement location = default;
        for (var index = 0; index < 3; index++) location = await LocationFixture.AddImageAsync(owner, id);
        var imageIds = LocationFixture.ImageIds(location);
        using var admitted = await ScoutingReportFixture.RequestAsync(owner, id, location.GetProperty("revision").GetInt64());

        using var host = Host(factory);
        await host.StartAsync(default);
        try
        {
            var operation = await ScoutingReportFixture.WaitForAsync(owner, admitted.Headers.Location, item => item.GetProperty("status").GetString() == "Succeeded");
            Assert.Equal("Succeeded", operation.GetProperty("status").GetString());
        }
        finally { await host.StopAsync(default); }

        var body = Assert.Single(bodies);
        Assert.Contains("Couple sessions at low tide", body);
        Assert.Contains("\"store\":false", body);
        Assert.Contains("json_schema", body);
        Assert.Contains("\"strict\":true", body);
        Assert.Equal(3, body.Split("input_image").Length - 1);
        Assert.DoesNotContain("\"tools\":", body);
        foreach (var secret in new[] { "SECRET-ADDRESS", "SECRET-LOCALITY", "51.487213", "-0.287604", "SECRET-NOTES", "SECRET-TAG" }) Assert.DoesNotContain(secret, body);

        using var response = await owner.GetAsync($"/api/locations/{id}/scouting-report");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        var saved = JsonSerializer.Deserialize<JsonElement>(raw);
        Assert.Equal("Live", saved.GetProperty("mode").GetString());
        Assert.Equal("location-scouting-v1", saved.GetProperty("promptVersion").GetString());
        Assert.False(string.IsNullOrEmpty(saved.GetProperty("model").GetString()));
        Assert.Equal("Couple sessions at low tide, two people.", saved.GetProperty("briefSnapshot").GetString());
        Assert.Equal(3, saved.GetProperty("imageCount").GetInt32());
        Assert.Equal(location.GetProperty("revision").GetInt64() > 0, saved.GetProperty("imageSetRevision").GetInt64() > 0);
        Assert.NotEqual(JsonValueKind.Null, saved.GetProperty("generatedAt").ValueKind);
        var report = saved.GetProperty("report");
        Assert.Equal(["overview", "suitability", "timesOfDay", "techniques", "groupSize", "cautions"], report.EnumerateObject().Select(property => property.Name).ToArray()!);
        Assert.Equal(2, report.GetProperty("overview").GetArrayLength());
        Assert.Equal(ScoutingReportFixture.ShootTypes, report.GetProperty("suitability").EnumerateArray().Select(entry => entry.GetProperty("shootType").GetString()!).ToArray());
        Assert.Equal("Not recommended", report.GetProperty("suitability")[4].GetProperty("rating").GetString());
        Assert.Equal(ScoutingReportFixture.Periods, report.GetProperty("timesOfDay").EnumerateArray().Select(entry => entry.GetProperty("period").GetString()!).ToArray());
        Assert.Equal("Recommended", report.GetProperty("timesOfDay")[4].GetProperty("rating").GetString());
        Assert.Equal("Visible", report.GetProperty("timesOfDay")[4].GetProperty("basis").GetString());
        Assert.Equal(["Leading lines", "Natural framing"], report.GetProperty("techniques").EnumerateArray().Select(entry => entry.GetProperty("technique").GetString()!).ToArray());
        Assert.Equal(6, report.GetProperty("groupSize").GetProperty("maximum").GetInt32());
        Assert.Single(report.GetProperty("cautions").EnumerateArray());
        var entries = report.GetProperty("overview").EnumerateArray().Concat(report.GetProperty("suitability").EnumerateArray())
            .Concat(report.GetProperty("timesOfDay").EnumerateArray()).Concat(report.GetProperty("techniques").EnumerateArray())
            .Append(report.GetProperty("groupSize")).Concat(report.GetProperty("cautions").EnumerateArray()).ToArray();
        foreach (var entry in entries)
            Assert.Equal(new[] { imageIds[0], imageIds[2] }, entry.GetProperty("citedImageIds").EnumerateArray().Select(value => value.GetGuid()).ToArray());
        Assert.DoesNotContain("score", raw, StringComparison.OrdinalIgnoreCase);

        var detail = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}");
        Assert.Equal("Ready", detail.GetProperty("reportStatus").GetString());
        Assert.Equal(raw, detail.GetProperty("report").GetRawText());
        Assert.Equal("SECRET-NOTES parking on Kew Green", detail.GetProperty("notes").GetString());
        var card = Assert.Single((await owner.GetFromJsonAsync<JsonElement>("/api/locations")).GetProperty("items").EnumerateArray());
        Assert.Equal("Ready", card.GetProperty("reportStatus").GetString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var hidden = await stranger.GetAsync($"/api/locations/{id}/scouting-report");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    public static TheoryData<string, Action<JsonObject>> InvalidShapes => new()
    {
        { "missing section", report => report.Remove("cautions") },
        { "foreign rating", report => report["suitability"]![0]!["rating"] = "Excellent" },
        { "foreign period", report => report["timesOfDay"]![0]!["period"] = "Noon" },
        { "blank reason", report => report["suitability"]![1]!["reason"] = "   " },
        { "overlength reason", report => report["cautions"]![0]!["caution"] = new string('x', 1001) },
        { "duplicate shoot type", report => report["suitability"]![1]!["shootType"] = "Portraits" },
        { "missing shoot type", report => report["suitability"]!.AsArray().RemoveAt(4) },
        { "four strengths", report => { var overview = report["overview"]!.AsArray(); overview.Add(overview[0]!.DeepClone()); overview.Add(overview[0]!.DeepClone()); } },
        { "thirteen techniques", report => { var techniques = report["techniques"]!.AsArray(); foreach (var name in new[] { "Rule of thirds", "Fill the frame", "Colour theory", "Near-far", "Simplify the scene", "Symmetry", "Negative space", "Layering", "Patterns and repetition", "Vantage point", "Leading lines" }) { var entry = techniques[0]!.DeepClone().AsObject(); entry["technique"] = name; techniques.Add(entry); } } },
        { "duplicate technique", report => report["techniques"]![1]!["technique"] = "Leading lines" },
        { "foreign technique", report => report["techniques"]![0]!["technique"] = "Golden ratio" },
        { "no Recommended period while one is supportable", report => report["timesOfDay"]![4]!["rating"] = "Unknown" },
        { "Recommended with an Inferred basis", report => report["timesOfDay"]![4]!["basis"] = "Inferred" },
        { "foreign basis", report => report["overview"]![0]!["basis"] = "Guessed" },
        { "foreign image", report => report["cautions"]![0]!["citedImages"] = new JsonArray(4) },
        { "no cited image", report => report["overview"]![0]!["citedImages"] = new JsonArray() },
        { "numeric score field", report => report["score"] = 7 },
        { "group minimum 0", report => report["groupSize"]!["minimum"] = 0 },
        { "group maximum 501", report => report["groupSize"]!["maximum"] = 501 },
        { "group minimum above maximum", report => report["groupSize"]!["minimum"] = 9 },
    };

    // L2-058, criterion 6; L2-034: each invalid shape retries once, then fails as invalid output with the current report unchanged.
    [Theory]
    [MemberData(nameof(InvalidShapes))]
    public async Task L2_058_6_An_invalid_shape_retries_once_then_fails_without_touching_the_current_report(string shape, Action<JsonObject> mutate)
    {
        Assert.NotNull(shape);
        var invalid = ScoutingReportFixture.Valid(1, 2, 3);
        mutate(invalid);
        var calls = 0;
        using var transport = new ControlledSourceTransport((_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(ScoutingReportFixture.Output(invalid));
        });
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await LocationFixture.CreateAsync(owner, new { name = "Kew Bridge foreshore" })).GetProperty("id").GetGuid();
        JsonElement location = default;
        for (var index = 0; index < 3; index++) location = await LocationFixture.AddImageAsync(owner, id);
        using var admitted = await ScoutingReportFixture.RequestAsync(owner, id, location.GetProperty("revision").GetInt64());
        using var host = Host(factory);
        await host.StartAsync(default);
        try
        {
            var retrying = await ScoutingReportFixture.WaitForAsync(owner, admitted.Headers.Location, item => item.GetProperty("failureCode").ValueKind == JsonValueKind.String);
            Assert.Equal("Queued", retrying.GetProperty("status").GetString());
            Assert.Equal("invalid_output", retrying.GetProperty("failureCode").GetString());
            Assert.Equal(1, calls);
            factory.Clock.Advance(TimeSpan.FromSeconds(5));
            var failed = await ScoutingReportFixture.WaitForAsync(owner, admitted.Headers.Location, item => item.GetProperty("status").GetString() == "Failed");
            Assert.Equal("Failed", failed.GetProperty("status").GetString());
            Assert.Equal("invalid_output", failed.GetProperty("failureCode").GetString());
            Assert.DoesNotContain("SECRET", failed.GetProperty("message").GetString());
        }
        finally { await host.StopAsync(default); }
        Assert.Equal(2, calls);
        using var none = await owner.GetAsync($"/api/locations/{id}/scouting-report");
        Assert.Equal(HttpStatusCode.NoContent, none.StatusCode);
        var detail = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}");
        Assert.Equal("Failed", detail.GetProperty("reportStatus").GetString());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("report").ValueKind);
    }

    // L2-060, criteria 4 and 5: an unchanged input reuses the completed report without a provider call; Regenerate creates one new job while the previous report stays visible.
    [Fact]
    public async Task L2_060_5_L2_060_4_Unchanged_input_reuses_the_report_and_regenerate_replaces_it_only_on_commit()
    {
        var calls = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var transport = new ControlledSourceTransport(async (_, _) =>
        {
            var call = Interlocked.Increment(ref calls);
            if (call > 1) await release.Task;
            var report = ScoutingReportFixture.Valid(1);
            report["overview"]![0]!["strength"] = call == 1 ? "First report" : "Second report";
            return ScoutingReportFixture.Output(report);
        });
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await LocationFixture.CreateAsync(owner, new { name = "Kew Bridge foreshore", notes = "Keep these notes." })).GetProperty("id").GetGuid();
        var location = await LocationFixture.AddImageAsync(owner, id);
        var revision = location.GetProperty("revision").GetInt64();
        using var admitted = await ScoutingReportFixture.RequestAsync(owner, id, revision);
        using var host = Host(factory);
        await host.StartAsync(default);
        try
        {
            await ScoutingReportFixture.WaitForAsync(owner, admitted.Headers.Location, item => item.GetProperty("status").GetString() == "Succeeded");
            var first = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}/scouting-report");
            Assert.Equal("First report", first.GetProperty("report").GetProperty("overview")[0].GetProperty("strength").GetString());
            var firstOperation = (await owner.GetFromJsonAsync<JsonElement>(admitted.Headers.Location)).GetProperty("id").GetGuid();
            revision = (await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}")).GetProperty("revision").GetInt64();

            using var reused = await ScoutingReportFixture.RequestAsync(owner, id, revision);
            Assert.Equal(firstOperation, (await reused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
            await Task.Delay(1500);
            Assert.Equal(1, calls);

            using var regenerate = await ScoutingReportFixture.RequestAsync(owner, id, revision, regenerate: true);
            var second = await regenerate.Content.ReadFromJsonAsync<JsonElement>();
            Assert.NotEqual(firstOperation, second.GetProperty("id").GetGuid());
            await ScoutingReportFixture.WaitForAsync(owner, regenerate.Headers.Location, item => item.GetProperty("status").GetString() == "Running");
            var whileRunning = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}");
            Assert.Equal("Running", whileRunning.GetProperty("reportStatus").GetString());
            Assert.Equal("First report", whileRunning.GetProperty("report").GetProperty("report").GetProperty("overview")[0].GetProperty("strength").GetString());
            using var active = await ScoutingReportFixture.RequestAsync(owner, id, whileRunning.GetProperty("revision").GetInt64(), regenerate: true);
            Assert.Equal(second.GetProperty("id").GetGuid(), (await active.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
            release.TrySetResult();
            await ScoutingReportFixture.WaitForAsync(owner, regenerate.Headers.Location, item => item.GetProperty("status").GetString() == "Succeeded");
        }
        finally { release.TrySetResult(); await host.StopAsync(default); }
        var replaced = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}");
        Assert.Equal("Ready", replaced.GetProperty("reportStatus").GetString());
        Assert.Equal("Second report", replaced.GetProperty("report").GetProperty("report").GetProperty("overview")[0].GetProperty("strength").GetString());
        Assert.Equal("Keep these notes.", replaced.GetProperty("notes").GetString());
        Assert.Equal(2, calls);
    }

    // L2-060, criterion 6; L2-034: transient failures retry within the budget, a refusal fails without retry, and the earlier report stays readable.
    [Fact]
    public async Task L2_060_6_Provider_failures_follow_the_retry_classes_and_keep_the_earlier_report_readable()
    {
        var responses = new Queue<Func<HttpResponseMessage>>([
            () => ScoutingReportFixture.Output(ScoutingReportFixture.Valid(1)),
            () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            () => new HttpResponseMessage(HttpStatusCode.TooManyRequests),
            ScoutingReportFixture.Refusal
        ]);
        var calls = 0;
        using var transport = new ControlledSourceTransport((_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(responses.Dequeue()());
        });
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await LocationFixture.CreateAsync(owner, new { name = "Kew Bridge foreshore" })).GetProperty("id").GetGuid();
        var location = await LocationFixture.AddImageAsync(owner, id);
        using var admitted = await ScoutingReportFixture.RequestAsync(owner, id, location.GetProperty("revision").GetInt64());
        using var host = Host(factory);
        await host.StartAsync(default);
        try
        {
            await ScoutingReportFixture.WaitForAsync(owner, admitted.Headers.Location, item => item.GetProperty("status").GetString() == "Succeeded");
            var revision = (await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}")).GetProperty("revision").GetInt64();
            using var regenerate = await ScoutingReportFixture.RequestAsync(owner, id, revision, regenerate: true);
            var transient = await ScoutingReportFixture.WaitForAsync(owner, regenerate.Headers.Location, item => item.GetProperty("failureCode").ValueKind == JsonValueKind.String);
            Assert.Equal("Queued", transient.GetProperty("status").GetString());
            Assert.Equal("provider_unavailable", transient.GetProperty("failureCode").GetString());
            factory.Clock.Advance(TimeSpan.FromSeconds(5));
            var limited = await ScoutingReportFixture.WaitForAsync(owner, regenerate.Headers.Location, item => item.GetProperty("failureCode").GetString() == "provider_rate_limited");
            Assert.Equal("Queued", limited.GetProperty("status").GetString());
            factory.Clock.Advance(TimeSpan.FromSeconds(30));
            var refused = await ScoutingReportFixture.WaitForAsync(owner, regenerate.Headers.Location, item => item.GetProperty("status").GetString() == "Failed");
            Assert.Equal("Failed", refused.GetProperty("status").GetString());
            Assert.Equal("unsupported_input", refused.GetProperty("failureCode").GetString());
        }
        finally { await host.StopAsync(default); }
        Assert.Equal(4, calls);
        var detail = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}");
        Assert.Equal("Failed", detail.GetProperty("reportStatus").GetString());
        Assert.Equal("Even open shade along the foreshore", detail.GetProperty("report").GetProperty("report").GetProperty("overview")[0].GetProperty("strength").GetString());
        using var readable = await owner.GetAsync($"/api/locations/{id}/scouting-report");
        Assert.Equal(HttpStatusCode.OK, readable.StatusCode);
    }

    private ApiFactory Factory(HttpMessageHandler transport) => new(database.ConnectionString, database.MediaRoot)
    { AiTransport = transport, Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only" } };

    private static AnalysisWorker Host(ApiFactory factory) => new(factory.Services.GetRequiredService<IServiceScopeFactory>(),
        factory.Services.GetRequiredService<IOptions<AiOptions>>(), NullLogger<AnalysisWorker>.Instance);
}
