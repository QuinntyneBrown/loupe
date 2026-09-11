// Given an owned photograph, when its library summary is read, then current job
// state and the availability of a saved critique are independently accurate.
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Critiques;
using Loupe.Application.Critiques;
using Loupe.Application.Operations;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class PhotographCritiqueSummaryTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_003_2_List_tracks_lifecycle_failed_replacement_and_cached_result_reuse()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var provider = new ControlledCritiqueProvider(async (_, _, token) =>
        {
            if (Interlocked.Increment(ref calls) > 1)
                throw new ProviderFailureException(ProviderFailureKind.InvalidCredentials);
            started.TrySetResult();
            await release.Task.WaitAsync(token);
            return CritiqueResultFixture.Valid();
        });
        await using var factory = CreateFactory(provider);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        await AssertSummaryAsync(client, id, false, null);
        await SubmitAsync(client, id);
        await AssertSummaryAsync(client, id, false, "Queued");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var run = scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCritiqueCommand());
            try
            {
                await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await AssertSummaryAsync(client, id, false, "Running");
            }
            finally { release.TrySetResult(); }
            Assert.True(await run);
        }
        await AssertSummaryAsync(client, id, true, "Succeeded");
        await SubmitAsync(client, id, true);
        await AssertSummaryAsync(client, id, true, "Queued");
        await RunAsync(factory);
        await AssertSummaryAsync(client, id, true, "Failed");
        await SubmitAsync(client, id);
        await AssertSummaryAsync(client, id, true, "Succeeded");
    }

    [Fact]
    public async Task L2_003_2_List_distinguishes_failure_without_saved_content_and_isolates_owners()
    {
        var provider = new ControlledCritiqueProvider((_, _, _) => throw new ProviderFailureException(ProviderFailureKind.InvalidCredentials));
        await using var factory = CreateFactory(provider);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(owner, "Private photograph")).GetProperty("id").GetGuid();
        var other = (await PhotographFixture.UploadAsync(stranger, "Other photograph")).GetProperty("id").GetGuid();
        await SubmitAsync(owner, id);
        await RunAsync(factory);
        await AssertSummaryAsync(owner, id, false, "Failed");
        await AssertSummaryAsync(stranger, other, false, null);
        var page = await stranger.GetFromJsonAsync<JsonElement>("/api/photographs");
        Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.DoesNotContain(id.ToString(), page.GetRawText());
    }

    private ApiFactory CreateFactory(ICritiqueProvider provider) => new(database.ConnectionString, database.MediaRoot)
    { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only-key" }, CritiqueProvider = provider };

    private static async Task AssertSummaryAsync(HttpClient client, Guid id, bool hasCritique, string? status)
    {
        var page = await client.GetFromJsonAsync<JsonElement>("/api/photographs");
        var item = Assert.Single(page.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);
        Assert.Equal(hasCritique, item.GetProperty("hasCritique").GetBoolean());
        Assert.Equal(status, item.GetProperty("critiqueStatus").GetString());
    }

    private static async Task SubmitAsync(HttpClient client, Guid id, bool regenerate = false)
    {
        var photograph = await client.GetFromJsonAsync<JsonElement>($"/api/photographs/{id}");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{id}/critique")
        { Content = JsonContent.Create(new { revision = photograph.GetProperty("revision").GetInt64(), regenerate }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static async Task RunAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCritiqueCommand()));
    }
}
