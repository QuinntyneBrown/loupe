using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
// Traces to: L2-060, L2-056, L2-055, L2-031, L2-033, L2-034
// Description: A report outdates when the image set changes but stays readable, a
// brief edit keeps it current, deletion or an image change cancels the active job
// so a late run commits nothing, and a failed job retries through the shared
// operations route with the same input only.
public sealed class ScoutingLifecycleTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    // L2-060, criterion 3; L2-056, criterion 5: adding or removing an image labels the report Outdated with the count it used, keeps it readable, and Regenerate restores Ready.
    [Fact]
    public async Task L2_060_3_L2_056_5_Changing_the_image_set_outdates_the_report_without_removing_it()
    {
        using var transport = new ControlledSourceTransport((_, _) => Task.FromResult(ScoutingReportFixture.Output(ScoutingReportFixture.Valid(1))));
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await LocationFixture.CreateAsync(owner, new { name = "Kew Bridge foreshore" })).GetProperty("id").GetGuid();
        JsonElement location = default;
        for (var index = 0; index < 2; index++) location = await LocationFixture.AddImageAsync(owner, id);
        using var admitted = await ScoutingReportFixture.RequestAsync(owner, id, location.GetProperty("revision").GetInt64());
        using var host = Host(factory);
        await host.StartAsync(default);
        try
        {
            await ScoutingReportFixture.WaitForAsync(owner, admitted.Headers.Location, item => item.GetProperty("status").GetString() == "Succeeded");
            var ready = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}");
            Assert.Equal("Ready", ready.GetProperty("reportStatus").GetString());
            var generatedAt = ready.GetProperty("report").GetProperty("generatedAt").GetDateTimeOffset();

            var added = await LocationFixture.AddImageAsync(owner, id);
            Assert.Equal("Outdated", added.GetProperty("reportStatus").GetString());
            Assert.Equal(2, added.GetProperty("report").GetProperty("imageCount").GetInt32());
            Assert.Equal(generatedAt, added.GetProperty("report").GetProperty("generatedAt").GetDateTimeOffset());
            var card = Assert.Single((await owner.GetFromJsonAsync<JsonElement>("/api/locations")).GetProperty("items").EnumerateArray());
            Assert.Equal("Outdated", card.GetProperty("reportStatus").GetString());

            var removedId = LocationFixture.ImageIds(added)[2];
            using var removed = await owner.DeleteAsync($"/api/locations/{id}/images/{removedId}?revision={added.GetProperty("revision").GetInt64()}");
            Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
            var afterRemoval = await removed.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Outdated", afterRemoval.GetProperty("reportStatus").GetString());
            using var readable = await owner.GetAsync($"/api/locations/{id}/scouting-report");
            Assert.Equal(HttpStatusCode.OK, readable.StatusCode);

            using var regenerate = await ScoutingReportFixture.RequestAsync(owner, id, afterRemoval.GetProperty("revision").GetInt64(), regenerate: true);
            await ScoutingReportFixture.WaitForAsync(owner, regenerate.Headers.Location, item => item.GetProperty("status").GetString() == "Succeeded");
            var current = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}");
            Assert.Equal("Ready", current.GetProperty("reportStatus").GetString());
            Assert.True(current.GetProperty("report").GetProperty("generatedAt").GetDateTimeOffset() >= generatedAt);
        }
        finally { await host.StopAsync(default); }
    }

    // L2-055, criterion 5: editing the brief leaves the report current and its brief snapshot identifies the brief it used.
    [Fact]
    public async Task L2_055_5_Editing_the_brief_keeps_the_report_current_with_its_snapshot()
    {
        using var transport = new ControlledSourceTransport((_, _) => Task.FromResult(ScoutingReportFixture.Output(ScoutingReportFixture.Valid(1))));
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await LocationFixture.CreateAsync(owner, new { name = "Kew Bridge foreshore", scoutingBrief = "First brief." })).GetProperty("id").GetGuid();
        var location = await LocationFixture.AddImageAsync(owner, id);
        using var admitted = await ScoutingReportFixture.RequestAsync(owner, id, location.GetProperty("revision").GetInt64());
        using var host = Host(factory);
        await host.StartAsync(default);
        try { await ScoutingReportFixture.WaitForAsync(owner, admitted.Headers.Location, item => item.GetProperty("status").GetString() == "Succeeded"); }
        finally { await host.StopAsync(default); }
        var revision = (await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}")).GetProperty("revision").GetInt64();
        using var edited = await owner.PutAsJsonAsync($"/api/locations/{id}/scouting-brief", new { revision, text = "Second brief." });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var detail = await edited.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Ready", detail.GetProperty("reportStatus").GetString());
        Assert.Equal("Second brief.", detail.GetProperty("scoutingBrief").GetString());
        Assert.Equal("First brief.", detail.GetProperty("report").GetProperty("briefSnapshot").GetString());
    }

    // L2-031, criterion 7; L2-060, criterion 3: deletion or an image change cancels the active job, and a run that finishes afterwards commits nothing.
    [Theory]
    [InlineData("delete")]
    [InlineData("add image")]
    [InlineData("remove image")]
    public async Task L2_031_7_A_late_run_cannot_commit_after_deletion_or_an_image_change(string change)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var transport = new ControlledSourceTransport(async (_, _) =>
        {
            started.TrySetResult();
            await release.Task;
            return ScoutingReportFixture.Output(ScoutingReportFixture.Valid(1));
        });
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await LocationFixture.CreateAsync(owner, new { name = "Kew Bridge foreshore" })).GetProperty("id").GetGuid();
        JsonElement location = default;
        for (var index = 0; index < 2; index++) location = await LocationFixture.AddImageAsync(owner, id);
        using var admitted = await ScoutingReportFixture.RequestAsync(owner, id, location.GetProperty("revision").GetInt64());
        using var host = Host(factory);
        await host.StartAsync(default);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var revision = location.GetProperty("revision").GetInt64();
            using var mutation = change switch
            {
                "delete" => await owner.DeleteAsync($"/api/locations/{id}?revision={revision}"),
                "add image" => await LocationFixture.SubmitImageAsync(owner, id),
                _ => await owner.DeleteAsync($"/api/locations/{id}/images/{LocationFixture.ImageIds(location)[0]}?revision={revision}")
            };
            mutation.EnsureSuccessStatusCode();
            var canceled = await owner.GetFromJsonAsync<JsonElement>(admitted.Headers.Location);
            Assert.Equal("Canceled", canceled.GetProperty("status").GetString());
            release.TrySetResult();
            await Task.Delay(1500);
            var afterwards = await owner.GetFromJsonAsync<JsonElement>(admitted.Headers.Location);
            Assert.Equal("Canceled", afterwards.GetProperty("status").GetString());
        }
        finally { release.TrySetResult(); await host.StopAsync(default); }
        if (change == "delete")
        {
            using var gone = await owner.GetAsync($"/api/locations/{id}");
            Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        }
        else
        {
            using var none = await owner.GetAsync($"/api/locations/{id}/scouting-report");
            Assert.Equal(HttpStatusCode.NoContent, none.StatusCode);
            Assert.Equal("None", (await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}")).GetProperty("reportStatus").GetString());
        }
    }

    // L2-060, criterion 6; L2-034, criterion 4: a failed job retries through the shared operations route with the same input; a changed input needs a new request.
    [Fact]
    public async Task L2_060_6_L2_034_4_A_failed_job_retries_with_the_same_input_and_rejects_changed_inputs()
    {
        var calls = 0;
        using var transport = new ControlledSourceTransport((_, _) => Task.FromResult(Interlocked.Increment(ref calls) <= 3
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : ScoutingReportFixture.Output(ScoutingReportFixture.Valid(1))));
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await LocationFixture.CreateAsync(owner, new { name = "Kew Bridge foreshore" })).GetProperty("id").GetGuid();
        var location = await LocationFixture.AddImageAsync(owner, id);
        using var admitted = await ScoutingReportFixture.RequestAsync(owner, id, location.GetProperty("revision").GetInt64());
        using var host = Host(factory);
        await host.StartAsync(default);
        try
        {
            await ScoutingReportFixture.WaitForAsync(owner, admitted.Headers.Location, item => item.GetProperty("failureCode").GetString() == "provider_unavailable");
            factory.Clock.Advance(TimeSpan.FromSeconds(5));
            await Task.Delay(1500);
            factory.Clock.Advance(TimeSpan.FromSeconds(30));
            var failed = await ScoutingReportFixture.WaitForAsync(owner, admitted.Headers.Location, item => item.GetProperty("status").GetString() == "Failed");
            Assert.Equal("Failed", failed.GetProperty("status").GetString());
            Assert.Equal(3, calls);
            var detail = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}");
            Assert.Equal("Failed", detail.GetProperty("reportStatus").GetString());

            using var stale = await Retry(owner, failed.GetProperty("id").GetGuid(), detail.GetProperty("revision").GetInt64() + 1);
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            using var retried = await Retry(owner, failed.GetProperty("id").GetGuid(), detail.GetProperty("revision").GetInt64());
            Assert.Equal(HttpStatusCode.Accepted, retried.StatusCode);
            var replacement = await retried.Content.ReadFromJsonAsync<JsonElement>();
            Assert.NotEqual(failed.GetProperty("id").GetGuid(), replacement.GetProperty("id").GetGuid());
            Assert.Equal("LocationScouting", replacement.GetProperty("type").GetString());
            await ScoutingReportFixture.WaitForAsync(owner, retried.Headers.Location, item => item.GetProperty("status").GetString() == "Succeeded");
            Assert.Equal("Ready", (await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}")).GetProperty("reportStatus").GetString());
            Assert.Equal(4, calls);

            using var unavailable = await Retry(owner, replacement.GetProperty("id").GetGuid(), (await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}")).GetProperty("revision").GetInt64());
            Assert.Equal(HttpStatusCode.Conflict, unavailable.StatusCode);
            Assert.Equal("retry_unavailable", (await unavailable.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
        finally { await host.StopAsync(default); }

        // A changed image set after a failure requires a new request rather than a retry.
        using var secondTransport = new ControlledSourceTransport((_, _) => Task.FromResult(ScoutingReportFixture.Refusal()));
        await using var second = Factory(secondTransport);
        using var client = await second.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var other = (await LocationFixture.CreateAsync(client, new { name = "Other" })).GetProperty("id").GetGuid();
        var otherLocation = await LocationFixture.AddImageAsync(client, other);
        using var otherAdmitted = await ScoutingReportFixture.RequestAsync(client, other, otherLocation.GetProperty("revision").GetInt64());
        using var secondHost = Host(second);
        await secondHost.StartAsync(default);
        JsonElement refused;
        try { refused = await ScoutingReportFixture.WaitForAsync(client, otherAdmitted.Headers.Location, item => item.GetProperty("status").GetString() == "Failed"); }
        finally { await secondHost.StopAsync(default); }
        Assert.Equal("unsupported_input", refused.GetProperty("failureCode").GetString());
        var current = await client.GetFromJsonAsync<JsonElement>($"/api/locations/{other}");
        using var notRetryable = await Retry(client, refused.GetProperty("id").GetGuid(), current.GetProperty("revision").GetInt64());
        Assert.Equal(HttpStatusCode.Conflict, notRetryable.StatusCode);
        Assert.Equal("retry_unavailable", (await notRetryable.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    // L2-034, criterion 4: a retryable failure whose input has since changed is rejected as changed inputs.
    [Fact]
    public async Task L2_034_4_A_retry_after_the_image_set_changed_is_rejected_as_changed_inputs()
    {
        var calls = 0;
        using var transport = new ControlledSourceTransport((_, _) => Task.FromResult(Interlocked.Increment(ref calls) <= 3
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : ScoutingReportFixture.Output(ScoutingReportFixture.Valid(1))));
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await LocationFixture.CreateAsync(owner, new { name = "Kew Bridge foreshore" })).GetProperty("id").GetGuid();
        var location = await LocationFixture.AddImageAsync(owner, id);
        using var admitted = await ScoutingReportFixture.RequestAsync(owner, id, location.GetProperty("revision").GetInt64());
        using var host = Host(factory);
        await host.StartAsync(default);
        JsonElement failed;
        try
        {
            await ScoutingReportFixture.WaitForAsync(owner, admitted.Headers.Location, item => item.GetProperty("failureCode").GetString() == "provider_unavailable");
            factory.Clock.Advance(TimeSpan.FromSeconds(5));
            await Task.Delay(1500);
            factory.Clock.Advance(TimeSpan.FromSeconds(30));
            failed = await ScoutingReportFixture.WaitForAsync(owner, admitted.Headers.Location, item => item.GetProperty("status").GetString() == "Failed");
        }
        finally { await host.StopAsync(default); }
        Assert.Equal("Failed", failed.GetProperty("status").GetString());
        var changed = await LocationFixture.AddImageAsync(owner, id);
        using var rejected = await Retry(owner, failed.GetProperty("id").GetGuid(), changed.GetProperty("revision").GetInt64());
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal("analysis_inputs_changed", (await rejected.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(3, calls);
    }

    private static async Task<HttpResponseMessage> Retry(HttpClient client, Guid operationId, long revision)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/operations/{operationId}/retry") { Content = JsonContent.Create(new { revision }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    private ApiFactory Factory(HttpMessageHandler transport) => new(database.ConnectionString, database.MediaRoot)
    { AiTransport = transport, Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only" } };

    private static AnalysisWorker Host(ApiFactory factory) => new(factory.Services.GetRequiredService<IServiceScopeFactory>(),
        factory.Services.GetRequiredService<IOptions<AiOptions>>(), NullLogger<AnalysisWorker>.Instance);
}
