// Given classified provider failures, when a replacement runs, then transient
// retries obey Retry-After and permanent failures preserve the saved critique.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Application.Critiques;
using Loupe.Application.Operations;
using Loupe.Api.Tests.Photographs;
using Loupe.Domain.Critiques;
using Loupe.Domain.Operations;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.Critiques;

public sealed class CritiqueProviderFailureTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData("Transient", 0, 3, "provider_unavailable")]
    [InlineData("RateLimited", 20, 3, "provider_rate_limited")]
    [InlineData("RateLimited", 300, 3, "provider_rate_limited")]
    [InlineData("RateLimited", 301, 1, "provider_rate_limited")]
    [InlineData("RateLimited", 0, 3, "provider_rate_limited")]
    [InlineData("RateLimited", -1, 3, "provider_rate_limited")]
    [InlineData("Transient", 301, 1, "provider_unavailable")]
    [InlineData("AccessDenied", 0, 1, "provider_access_denied")]
    [InlineData("InvalidCredentials", 0, 1, "provider_credentials")]
    [InlineData("UnsupportedInput", 0, 1, "unsupported_input")]
    [InlineData("Disabled", 0, 1, "provider_disabled")]
    [InlineData("InvalidOutput", 0, 2, "invalid_output")]
    public async Task L2_034_1_3_6_Classified_failures_have_bounded_retries_and_safe_status(string kind, int retryAfter, int attempts, string code)
    {
        var provider = new ControlledCritiqueProvider((call, _, _) => call == 1
            ? Task.FromResult(CritiqueResultFixture.Valid())
            : Task.FromException<CritiqueResult>(new ProviderFailureException(Enum.Parse<ProviderFailureKind>(kind),
                retryAfter == 0 ? null : TimeSpan.FromSeconds(retryAfter))));
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot)
        { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Demo" }, CritiqueProvider = provider };
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var original = await SubmitAsync(client, id, 1);
        Assert.Equal(HttpStatusCode.Accepted, original.StatusCode);
        Assert.True(await RunAsync(factory));
        var saved = await client.GetStringAsync($"/api/photographs/{id}/critique");
        using var replacement = await SubmitAsync(client, id, 2);
        Assert.Equal(HttpStatusCode.Accepted, replacement.StatusCode);
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            Assert.True(await RunAsync(factory));
            var status = await client.GetFromJsonAsync<JsonElement>(replacement.Headers.Location);
            Assert.Equal(code, status.GetProperty("failureCode").GetString());
            Assert.Equal(saved, await client.GetStringAsync($"/api/photographs/{id}/critique"));
            if (attempt < attempts)
            {
                Assert.Equal("Queued", status.GetProperty("status").GetString());
                var wait = Math.Max(retryAfter, attempt == 1 ? 5 : 30);
                Assert.InRange(status.GetProperty("nextAttemptAt").GetDateTimeOffset(),
                    factory.Clock.GetUtcNow().AddSeconds(wait).AddTicks(-9), factory.Clock.GetUtcNow().AddSeconds(wait));
                Assert.False(await RunAsync(factory));
                factory.Clock.Advance(TimeSpan.FromSeconds(wait));
            }
            else
            {
                Assert.Equal("Failed", status.GetProperty("status").GetString());
                Assert.Equal(JsonValueKind.Null, status.GetProperty("nextAttemptAt").ValueKind);
                if (retryAfter > 300)
                    Assert.InRange(status.GetProperty("retryAvailableAt").GetDateTimeOffset(),
                        factory.Clock.GetUtcNow().AddSeconds(retryAfter).AddTicks(-9), factory.Clock.GetUtcNow().AddSeconds(retryAfter));
            }
        }
        Assert.False(await RunAsync(factory));
        Assert.Equal(attempts + 1, provider.Calls);
    }

    [Theory]
    [InlineData("InvalidOutput", "Transient", "InvalidOutput")]
    [InlineData("Transient", "InvalidOutput", "Transient")]
    public async Task L2_034_3_Mixed_failure_classes_share_the_same_attempt_budget(string first, string second, string third)
    {
        var kinds = new[] { first, second, third };
        var provider = new ControlledCritiqueProvider((call, _, _) => call == 1 ? Task.FromResult(CritiqueResultFixture.Valid())
            : Task.FromException<CritiqueResult>(new ProviderFailureException(Enum.Parse<ProviderFailureKind>(kinds[call - 2]))));
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot)
        { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Demo" }, CritiqueProvider = provider };
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var original = await SubmitAsync(client, id, 1);
        Assert.Equal(HttpStatusCode.Accepted, original.StatusCode);
        Assert.True(await RunAsync(factory));
        var saved = await client.GetStringAsync($"/api/photographs/{id}/critique");
        using var replacement = await SubmitAsync(client, id, 2);
        Assert.Equal(HttpStatusCode.Accepted, replacement.StatusCode);
        for (var index = 0; index < kinds.Length; index++)
        {
            Assert.True(await RunAsync(factory));
            var status = await client.GetFromJsonAsync<JsonElement>(replacement.Headers.Location);
            Assert.Equal(index == 2 ? "Failed" : "Queued", status.GetProperty("status").GetString());
            if (index < 2) factory.Clock.Advance(TimeSpan.FromSeconds(index == 0 || kinds[index] == "InvalidOutput" ? 5 : 30));
        }
        Assert.False(await RunAsync(factory));
        Assert.Equal(4, provider.Calls);
        Assert.Equal(saved, await client.GetStringAsync($"/api/photographs/{id}/critique"));
    }

    private static async Task<bool> RunAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCritiqueCommand());
    }

    private static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid id, long revision)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{id}/critique")
        { Content = JsonContent.Create(new { revision, regenerate = true }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }
}
