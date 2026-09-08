// Given a provider that ignores cancellation, when 120 seconds elapse, then the
// attempt ends, retries follow 5/30-second waits and no fourth call is made.
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Application.Critiques;
using Loupe.Api.Tests.Photographs;
using Loupe.Domain.Critiques;
using Loupe.Domain.Operations;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Loupe.Api.Tests.Critiques;

public sealed class CritiqueTimeoutTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task L2_034_1_2_Never_returning_calls_timeout_and_share_a_three_attempt_budget(bool succeedsOnThird)
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var pending = new ConcurrentQueue<(TaskCompletionSource<CritiqueResult> Completion, CancellationToken Token)>();
        var provider = new ControlledCritiqueProvider((call, _, token) =>
        {
            var completion = new TaskCompletionSource<CritiqueResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Enqueue((completion, token));
            return succeedsOnThird && call == 3 ? Task.FromResult(CritiqueResultFixture.Valid()) : completion.Task;
        });
        await using var factory = CreateFactory(provider, clock);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        var location = await AdmitAsync(client, id);
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            using var stopping = new CancellationTokenSource();
            var run = scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCritiqueCommand(), stopping.Token);
            try
            {
                await WaitForCallsAsync(provider, attempt);
                if (!(succeedsOnThird && attempt == 3))
                {
                    for (var tick = 0; tick < 5; tick++)
                    {
                        clock.Advance(TimeSpan.FromSeconds(20));
                        await WaitForRenewalAsync(client, location, clock.GetUtcNow());
                    }
                    clock.Advance(TimeSpan.FromSeconds(19));
                    Assert.False(run.IsCompleted);
                    clock.Advance(TimeSpan.FromSeconds(1));
                    Assert.True(await run.WaitAsync(TimeSpan.FromSeconds(3)));
                    Assert.True(pending.Last().Token.IsCancellationRequested);
                }
                else Assert.True(await run.WaitAsync(TimeSpan.FromSeconds(3)));
                var status = await client.GetFromJsonAsync<JsonElement>(location);
                if (attempt < 3)
                {
                    Assert.Equal("Queued", status.GetProperty("status").GetString());
                    Assert.Equal("provider_timeout", status.GetProperty("failureCode").GetString());
                    var wait = TimeSpan.FromSeconds(attempt == 1 ? 5 : 30);
                    Assert.InRange(status.GetProperty("nextAttemptAt").GetDateTimeOffset(), clock.GetUtcNow().Add(wait).AddTicks(-9), clock.GetUtcNow().Add(wait));
                    Assert.Null(await scope.ServiceProvider.GetRequiredService<ICritiqueWorkStore>().ClaimAsync(ExecutionMode.Demo, default));
                    clock.Advance(wait);
                }
                else Assert.Equal(succeedsOnThird ? "Succeeded" : "Failed", status.GetProperty("status").GetString());
            }
            finally
            {
                stopping.Cancel();
                foreach (var call in pending) call.Completion.TrySetResult(CritiqueResultFixture.Valid());
                try { await run.WaitAsync(TimeSpan.FromSeconds(3)); }
                catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
            }
        }
        clock.Advance(TimeSpan.FromMinutes(5));
        await using var finalScope = factory.Services.CreateAsyncScope();
        Assert.False(await finalScope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCritiqueCommand()));
        Assert.Equal(3, provider.Calls);
        using var critique = await client.GetAsync($"/api/photographs/{id}/critique");
        Assert.Equal(succeedsOnThird ? HttpStatusCode.OK : HttpStatusCode.NoContent, critique.StatusCode);
        using var photograph = await client.GetAsync($"/api/photographs/{id}");
        Assert.Equal(HttpStatusCode.OK, photograph.StatusCode);
    }

    [Fact]
    public async Task L2_033_4_Deletion_ends_waiting_even_when_provider_ignores_cancellation()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var completion = new TaskCompletionSource<CritiqueResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new ControlledCritiqueProvider((_, _, _) => completion.Task);
        await using var factory = CreateFactory(provider, clock);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        var location = await AdmitAsync(client, id);
        await using var scope = factory.Services.CreateAsyncScope();
        var run = scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCritiqueCommand());
        try
        {
            await WaitForCallsAsync(provider, 1);
            using var deleted = await client.DeleteAsync($"/api/photographs/{id}?revision=1");
            deleted.EnsureSuccessStatusCode();
            clock.Advance(TimeSpan.FromSeconds(20));
            Assert.True(await run.WaitAsync(TimeSpan.FromSeconds(3)));
            completion.SetResult(CritiqueResultFixture.Valid());
            var status = await client.GetFromJsonAsync<JsonElement>(location);
            Assert.Equal("Canceled", status.GetProperty("status").GetString());
            using var absent = await client.GetAsync($"/api/photographs/{id}/critique");
            Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
        }
        finally { completion.TrySetResult(CritiqueResultFixture.Valid()); await run.WaitAsync(TimeSpan.FromSeconds(3)); }
    }

    private ApiFactory CreateFactory(ControlledCritiqueProvider provider, FakeTimeProvider clock) => new(database.ConnectionString, database.MediaRoot)
    { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Demo" }, CritiqueProvider = provider, ClockOverride = clock };

    private static async Task<Uri> AdmitAsync(HttpClient client, Guid id)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{id}/critique")
        { Content = JsonContent.Create(new { revision = 1 }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var admitted = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        return admitted.Headers.Location!;
    }

    private static async Task WaitForCallsAsync(ControlledCritiqueProvider provider, int count)
    {
        for (var attempt = 0; attempt < 30 && provider.Calls < count; attempt++) await Task.Delay(100);
        Assert.Equal(count, provider.Calls);
    }

    private static async Task WaitForRenewalAsync(HttpClient client, Uri location, DateTimeOffset now)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var status = await client.GetFromJsonAsync<JsonElement>(location);
            if (status.GetProperty("updatedAt").GetDateTimeOffset() >= now.AddTicks(-9)) return;
            await Task.Delay(100);
        }
        Assert.Fail("The active call did not renew its lease.");
    }
}
