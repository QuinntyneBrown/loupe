using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Critiques;
using Loupe.Api.Tests.Locations;
using Xunit;

namespace Loupe.Api.Tests.Scouting;

// Acceptance Test
// Traces to: L2-060, L2-033, L2-035
// Description: Requesting a scouting report admits one durable job for an imaged
// location without calling the provider, refuses a location with no images or a
// missing live configuration, returns the active job on repeat, and holds the
// five-active cap, the owner boundary, and the opened revision.
public sealed class ScoutingAdmissionTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private int providerCalls;

    private ApiFactory Factory(bool configured = true) => new(database.ConnectionString, database.MediaRoot)
    {
        Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = configured ? "fixture-only" : null },
        AiTransport = new ControlledAiTransport((_, _) =>
        {
            Interlocked.Increment(ref providerCalls);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        })
    };

    // L2-060, criterion 1; L2-033, criterion 1: an imaged location admits one durable job with a status location, without a provider call.
    [Fact]
    public async Task L2_060_1_L2_033_1_An_imaged_location_admits_one_durable_job_without_a_provider_call()
    {
        var subject = Guid.NewGuid().ToString();
        Guid id, operationId;
        string admittedBody;
        await using (var factory = Factory())
        {
            using var owner = await factory.CreateAuthenticatedClientAsync(subject);
            id = (await LocationFixture.CreateAsync(owner, new { name = "Kew Bridge foreshore", scoutingBrief = "Low tide couples." })).GetProperty("id").GetGuid();
            var location = await LocationFixture.AddImageAsync(owner, id);
            var revision = location.GetProperty("revision").GetInt64();
            var key = Guid.NewGuid().ToString();
            using var admitted = await Request(owner, id, revision, false, key);
            Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
            admittedBody = await admitted.Content.ReadAsStringAsync();
            var operation = JsonSerializer.Deserialize<JsonElement>(admittedBody);
            operationId = operation.GetProperty("id").GetGuid();
            Assert.Equal($"/api/operations/{operationId}", admitted.Headers.Location?.ToString());
            Assert.Equal("LocationScouting", operation.GetProperty("type").GetString());
            Assert.Equal("Queued", operation.GetProperty("status").GetString());
            Assert.Equal(id, operation.GetProperty("resourceId").GetGuid());
            Assert.Equal("Live", operation.GetProperty("mode").GetString());

            using var replay = await Request(owner, id, revision, false, key);
            Assert.Equal(admittedBody, await replay.Content.ReadAsStringAsync());
            using var repeat = await Request(owner, id, revision, false);
            Assert.Equal(HttpStatusCode.Accepted, repeat.StatusCode);
            Assert.Equal(operationId, (await repeat.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
            using var regenerateWhileActive = await Request(owner, id, revision, true);
            Assert.Equal(operationId, (await regenerateWhileActive.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());

            var detail = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}");
            Assert.Equal("Queued", detail.GetProperty("reportStatus").GetString());
            Assert.Equal(JsonValueKind.Null, detail.GetProperty("report").ValueKind);
            var current = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}/scouting-report/operation");
            Assert.Equal(operationId, current.GetProperty("id").GetGuid());
            using var report = await owner.GetAsync($"/api/locations/{id}/scouting-report");
            Assert.Equal(HttpStatusCode.NoContent, report.StatusCode);

            using var edit = await owner.PutAsJsonAsync($"/api/locations/{id}/notes", new { revision = detail.GetProperty("revision").GetInt64(), text = "Still editable while queued." });
            Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
            Assert.Equal(0, providerCalls);
        }
        await using var restart = Factory();
        using var later = await restart.CreateAuthenticatedClientAsync(subject);
        var durable = await later.GetFromJsonAsync<JsonElement>($"/api/operations/{operationId}");
        Assert.Equal("Queued", durable.GetProperty("status").GetString());
        Assert.Equal("Queued", (await later.GetFromJsonAsync<JsonElement>($"/api/locations/{id}")).GetProperty("reportStatus").GetString());
        var card = Assert.Single((await later.GetFromJsonAsync<JsonElement>("/api/locations")).GetProperty("items").EnumerateArray());
        Assert.Equal("Queued", card.GetProperty("reportStatus").GetString());
    }

    // L2-060, criterion 2: a location with no images is a field error naming images, and nothing is queued.
    [Fact]
    public async Task L2_060_2_A_location_without_images_is_a_field_error_without_queuing()
    {
        await using var factory = Factory();
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var id = (await LocationFixture.CreateAsync(owner)).GetProperty("id").GetGuid();
        using var rejected = await Request(owner, id, 1, false);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var error = await rejected.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_request", error.GetProperty("code").GetString());
        Assert.True(error.GetProperty("errors").TryGetProperty("images", out _), error.GetRawText());
        using var none = await owner.GetAsync($"/api/locations/{id}/scouting-report/operation");
        Assert.Equal(HttpStatusCode.NoContent, none.StatusCode);
        Assert.Equal("None", (await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}")).GetProperty("reportStatus").GetString());
        Assert.Equal(0, providerCalls);
    }

    // L2-060, criterion 7: without live credentials the API reports Integration not configured, queues nothing, and the location stays editable.
    [Fact]
    public async Task L2_060_7_Missing_credentials_report_integration_not_configured_without_queuing()
    {
        await using var factory = Factory(configured: false);
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var id = (await LocationFixture.CreateAsync(owner)).GetProperty("id").GetGuid();
        var location = await LocationFixture.AddImageAsync(owner, id);
        using var unavailable = await Request(owner, id, location.GetProperty("revision").GetInt64(), false);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Equal("integration_not_configured", (await unavailable.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.NotNull(unavailable.Headers.RetryAfter);
        using var none = await owner.GetAsync($"/api/locations/{id}/scouting-report/operation");
        Assert.Equal(HttpStatusCode.NoContent, none.StatusCode);
        using var edit = await owner.PutAsJsonAsync($"/api/locations/{id}/scouting-brief", new { revision = location.GetProperty("revision").GetInt64(), text = "Still editable." });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal("None", (await edit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reportStatus").GetString());
    }

    // L2-035: five active jobs reject the sixth with 429 and no side effect; foreign and stale requests change nothing.
    [Fact]
    public async Task L2_035_Five_active_jobs_reject_the_sixth_and_foreign_or_stale_requests_change_nothing()
    {
        await using var factory = Factory();
        using var owner = await factory.CreateAuthenticatedClientAsync();
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var locations = new List<(Guid Id, long Revision)>();
        for (var index = 0; index < 6; index++)
        {
            var id = (await LocationFixture.CreateAsync(owner, new { name = $"Location {index}" })).GetProperty("id").GetGuid();
            locations.Add((id, (await LocationFixture.AddImageAsync(owner, id)).GetProperty("revision").GetInt64()));
        }
        foreach (var (id, revision) in locations.Take(5))
        {
            using var admitted = await Request(owner, id, revision, false);
            Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        }
        var (sixthId, sixthRevision) = locations[5];
        using var limited = await Request(owner, sixthId, sixthRevision, false);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        using var none = await owner.GetAsync($"/api/locations/{sixthId}/scouting-report/operation");
        Assert.Equal(HttpStatusCode.NoContent, none.StatusCode);

        var (firstId, firstRevision) = locations[0];
        using var foreign = await Request(stranger, firstId, firstRevision, false);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var stale = await Request(owner, sixthId, sixthRevision + 1, false);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var invalid = await Request(owner, sixthId, 0, false);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(0, providerCalls);
    }

    private static async Task<HttpResponseMessage> Request(HttpClient client, Guid id, long revision, bool regenerate, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/locations/{id}/scouting-report") { Content = JsonContent.Create(new { revision, regenerate }) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }
}
