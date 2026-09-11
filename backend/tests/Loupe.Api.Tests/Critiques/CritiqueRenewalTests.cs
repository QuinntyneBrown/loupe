// Given a provider call that outlasts the initial lease, when the worker remains
// healthy, then it renews ownership; deletion cancels the call and stops renewal.
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

public sealed class CritiqueRenewalTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task L2_033_3_4_Long_running_call_keeps_ownership_and_deletion_stops_it(bool delete)
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new TaskCompletionSource<CritiqueResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new ControlledCritiqueProvider(async (_, _, cancellationToken) =>
        {
            entered.TrySetResult();
            try { return await completion.Task.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) { canceled.TrySetResult(); throw; }
        });
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot)
        { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only-key" }, CritiqueProvider = provider, ClockOverride = clock };
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{id}/critique")
        { Content = JsonContent.Create(new { revision = 1 }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var admitted = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        using var stopping = new CancellationTokenSource();
        var run = scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCritiqueCommand(), stopping.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            for (var tick = 0; tick < 4; tick++)
            {
                clock.Advance(TimeSpan.FromSeconds(20));
                await ExpectRenewalAsync(client, admitted.Headers.Location!, clock.GetUtcNow());
                await using var competitor = factory.Services.CreateAsyncScope();
                Assert.Null(await competitor.ServiceProvider.GetRequiredService<ICritiqueWorkStore>().ClaimAsync(ExecutionMode.Live, default));
                if (delete) break;
            }
            if (delete)
            {
                using var deleted = await client.DeleteAsync($"/api/photographs/{id}?revision=1");
                deleted.EnsureSuccessStatusCode();
                clock.Advance(TimeSpan.FromSeconds(20));
                await canceled.Task.WaitAsync(TimeSpan.FromSeconds(3));
            }
            else completion.SetResult(CritiqueResultFixture.Valid());
            Assert.True(await run.WaitAsync(TimeSpan.FromSeconds(3)));
            var terminal = await client.GetStringAsync(admitted.Headers.Location);
            Assert.Equal(delete ? "Canceled" : "Succeeded", JsonSerializer.Deserialize<JsonElement>(terminal).GetProperty("status").GetString());
            clock.Advance(TimeSpan.FromSeconds(100));
            Assert.Equal(terminal, await client.GetStringAsync(admitted.Headers.Location));
            Assert.Equal(1, provider.Calls);
        }
        finally
        {
            stopping.Cancel();
            completion.TrySetResult(CritiqueResultFixture.Valid());
            try { await run.WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
        }
    }

    private static async Task ExpectRenewalAsync(HttpClient client, Uri location, DateTimeOffset now)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var status = await client.GetFromJsonAsync<JsonElement>(location);
            Assert.Equal("Running", status.GetProperty("status").GetString());
            if (status.GetProperty("updatedAt").GetDateTimeOffset() >= now.AddTicks(-9)) return;
            await Task.Delay(100);
        }
        Assert.Fail("The running operation did not renew its lease.");
    }
}
