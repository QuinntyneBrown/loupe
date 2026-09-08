// Given a Live provider transport, when HTTP or response validation fails, then
// durable safe failure/retry states replace neither saved content nor private notes.
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Loupe.Api.Tests.Photographs;
using Loupe.Application.Critiques;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.Critiques;

public sealed class LiveCritiqueFailureTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData(401, "error", "Failed", "provider_credentials", 0)]
    [InlineData(403, "error", "Failed", "provider_access_denied", 0)]
    [InlineData(400, "error", "Failed", "unsupported_input", 0)]
    [InlineData(404, "error", "Failed", "provider_disabled", 0)]
    [InlineData(408, "error", "Queued", "provider_unavailable", 0)]
    [InlineData(429, "error", "Queued", "provider_rate_limited", 30)]
    [InlineData(429, "error", "Failed", "provider_rate_limited", 301)]
    [InlineData(500, "error", "Queued", "provider_unavailable", 0)]
    [InlineData(503, "error", "Queued", "provider_unavailable", 0)]
    [InlineData(0, "transport-timeout", "Queued", "provider_unavailable", 0)]
    [InlineData(200, "refusal", "Failed", "unsupported_input", 0)]
    [InlineData(200, "incomplete", "Queued", "invalid_output", 0)]
    [InlineData(200, "malformed", "Queued", "invalid_output", 0)]
    [InlineData(200, "oversized", "Queued", "invalid_output", 0)]
    public async Task L2_034_2_3_4_5_Provider_failures_have_bounded_safe_outcomes(int status, string responseKind, string expectedStatus, string code, int retryAfter)
    {
        var calls = 0;
        using var transport = new ControlledAiTransport((_, _) =>
        {
            calls++;
            if (responseKind == "transport-timeout") return Task.FromException<HttpResponseMessage>(new TaskCanceledException("private-provider-detail"));
            var body = responseKind switch
            {
                "refusal" => "{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"refusal\",\"refusal\":\"private-provider-detail\"}]}]}",
                "incomplete" => "{\"status\":\"incomplete\",\"output\":[]}",
                "oversized" => new string('x', 2_000_001),
                _ => "private-provider-detail"
            };
            var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            if (retryAfter > 0) response.Headers.RetryAfter = new(TimeSpan.FromSeconds(retryAfter));
            return Task.FromResult(response);
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
            var body = await client.GetStringAsync(admitted.Headers.Location);
            Assert.DoesNotContain("private-provider-detail", body);
            Assert.DoesNotContain("fixture-only-openai-key", body);
            var operation = JsonSerializer.Deserialize<JsonElement>(body);
            Assert.Equal(expectedStatus, operation.GetProperty("status").GetString());
            Assert.Equal(code, operation.GetProperty("failureCode").GetString());
            if (retryAfter > 0)
            {
                var timestamp = operation.GetProperty(expectedStatus == "Queued" ? "nextAttemptAt" : "retryAvailableAt").GetDateTimeOffset();
                Assert.InRange(timestamp, factory.Clock.GetUtcNow().AddSeconds(retryAfter).AddTicks(-9), factory.Clock.GetUtcNow().AddSeconds(retryAfter));
            }
            using var critique = await client.GetAsync($"/api/photographs/{id}/critique");
            Assert.Equal(HttpStatusCode.NoContent, critique.StatusCode);
        }
        finally
        {
            using var deleted = await client.DeleteAsync($"/api/photographs/{id}?revision=1");
            deleted.EnsureSuccessStatusCode();
        }
    }
}
