// Given a failed critique, when its owner retries the same inputs, then one new
// durable operation uses that snapshot; changed inputs and early retries are rejected.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Application.Critiques;
using Loupe.Application.Operations;
using Loupe.Api.Tests.Photographs;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.Critiques;

public sealed class CritiqueManualRetryTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task L2_034_4_5_Manual_retry_resolves_one_operation_and_rejects_changed_inputs(bool changedBrief)
    {
        var valid = CritiqueResultFixture.Valid();
        var provider = new ControlledCritiqueProvider((call, _, _) => Task.FromResult(call is 2 or 3 ? valid with { Exposure = null! } : valid));
        await using var factory = CreateFactory(provider);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var brief = await client.PutAsJsonAsync($"/api/photographs/{id}/brief", new { revision = 1, intent = "Original intent" });
        brief.EnsureSuccessStatusCode();
        using var original = await AdmitAsync(client, id, 2);
        await RunAsync(factory);
        var saved = await client.GetStringAsync($"/api/photographs/{id}/critique");
        using var replacement = await AdmitAsync(client, id, 3);
        await RunAsync(factory);
        factory.Clock.Advance(TimeSpan.FromSeconds(5));
        await RunAsync(factory);
        var failedId = (await replacement.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        if (changedBrief)
        {
            using var edited = await client.PutAsJsonAsync($"/api/photographs/{id}/brief", new { revision = 3, intent = "Different current intent" });
            edited.EnsureSuccessStatusCode();
            using var rejected = await RetryAsync(client, failedId, 4, Guid.NewGuid().ToString());
            Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
            Assert.Equal("analysis_inputs_changed", (await rejected.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            Assert.Equal(saved, await client.GetStringAsync($"/api/photographs/{id}/critique"));
            return;
        }
        using var notes = await client.PutAsJsonAsync($"/api/photographs/{id}/notes", new { revision = 3, notes = "Private retry journal" });
        notes.EnsureSuccessStatusCode();
        var key = Guid.NewGuid().ToString();
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => RetryAsync(client, failedId, 4, key)));
        var ids = new HashSet<Guid>();
        foreach (var response in responses)
            using (response)
            {
                Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
                ids.Add((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
            }
        Assert.Single(ids);
        Assert.DoesNotContain(failedId, ids);
        Assert.Equal(saved, await client.GetStringAsync($"/api/photographs/{id}/critique"));
        using var laterBrief = await client.PutAsJsonAsync($"/api/photographs/{id}/brief", new { revision = 4, intent = "After retry admission" });
        laterBrief.EnsureSuccessStatusCode();
        using var replay = await RetryAsync(client, failedId, 4, key);
        Assert.Equal(HttpStatusCode.Accepted, replay.StatusCode);
        Assert.Equal(ids.Single(), (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        await RunAsync(factory);
        var critique = await client.GetFromJsonAsync<JsonElement>($"/api/photographs/{id}/critique");
        Assert.Equal(ids.Single(), critique.GetProperty("operationId").GetGuid());
        Assert.Equal("Original intent", critique.GetProperty("brief").GetProperty("intent").GetString());
        var photo = await client.GetFromJsonAsync<JsonElement>($"/api/photographs/{id}");
        Assert.Equal("Private retry journal", photo.GetProperty("notes").GetString());
        Assert.Equal("After retry admission", photo.GetProperty("brief").GetProperty("intent").GetString());
        Assert.Equal(4, provider.Calls);
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        foreach (var target in new[] { failedId, Guid.NewGuid() })
        {
            using var hidden = await RetryAsync(stranger, target, 1, Guid.NewGuid().ToString());
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        }
    }

    [Theory]
    [InlineData(ProviderFailureKind.Disabled)]
    [InlineData(ProviderFailureKind.RateLimited)]
    public async Task L2_034_4_6_Manual_retry_respects_configuration_failures_and_provider_waits(ProviderFailureKind kind)
    {
        var provider = new ControlledCritiqueProvider((_, _, _) => throw new ProviderFailureException(kind, TimeSpan.FromSeconds(301)));
        await using var factory = CreateFactory(provider);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var admitted = await AdmitAsync(client, id, 1);
        var failedId = (await admitted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await RunAsync(factory);
        var key = Guid.NewGuid().ToString();
        using var early = await RetryAsync(client, failedId, 1, key);
        Assert.Equal(kind == ProviderFailureKind.Disabled ? HttpStatusCode.Conflict : HttpStatusCode.TooManyRequests, early.StatusCode);
        if (kind == ProviderFailureKind.RateLimited)
        {
            Assert.Equal(TimeSpan.FromSeconds(301), early.Headers.RetryAfter?.Delta);
            factory.Clock.Advance(TimeSpan.FromSeconds(301));
            using var retry = await RetryAsync(client, failedId, 1, key);
            Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode);
            using var deleted = await client.DeleteAsync($"/api/photographs/{id}?revision=1");
            deleted.EnsureSuccessStatusCode();
        }
    }

    private ApiFactory CreateFactory(ControlledCritiqueProvider provider) => new(database.ConnectionString, database.MediaRoot)
    { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Demo" }, CritiqueProvider = provider };

    private static async Task RunAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCritiqueCommand()));
    }

    private static async Task<HttpResponseMessage> AdmitAsync(HttpClient client, Guid id, long revision)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{id}/critique")
        { Content = JsonContent.Create(new { revision, regenerate = true }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return response;
    }

    private static async Task<HttpResponseMessage> RetryAsync(HttpClient client, Guid id, long revision, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/operations/{id}/retry")
        { Content = JsonContent.Create(new { revision }) };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }
}
