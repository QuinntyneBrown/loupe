// Given a backlog and blocked provider calls, when one or two worker hosts run,
// then configured capacity is filled without exceeding shared or per-owner caps.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Photographs;
using Loupe.Infrastructure.Ai;
using Loupe.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loupe.Api.Tests.Critiques;

public sealed class CritiqueWorkerCapacityTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData(1, 4)]
    [InlineData(2, 4)]
    [InlineData(2, 3)]
    public async Task L2_035_4_048_4_Hosts_fill_configured_capacity_and_drain_the_backlog(int hostCount, int capacity)
    {
        var gate = new object();
        var active = 0;
        var maximum = 0;
        var ownerMaximum = 0;
        var owners = new Dictionary<string, int>();
        var filled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new ControlledCritiqueProvider(async (_, input, cancellationToken) =>
        {
            var owner = input.Brief.Intent!;
            lock (gate)
            {
                active++;
                maximum = Math.Max(maximum, active);
                owners[owner] = owners.GetValueOrDefault(owner) + 1;
                ownerMaximum = Math.Max(ownerMaximum, owners[owner]);
                if (active >= capacity) filled.TrySetResult();
            }
            try
            {
                await release.Task.WaitAsync(cancellationToken);
                return CritiqueResultFixture.Valid();
            }
            finally { lock (gate) { active--; owners[owner]--; } }
        });
        var settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Demo", ["Ai:MaxConcurrentCalls"] = capacity.ToString() };
        await using var first = new ApiFactory(database.ConnectionString, database.MediaRoot) { Settings = settings, CritiqueProvider = provider };
        await using var second = new ApiFactory(database.ConnectionString, database.MediaRoot) { Settings = settings, CritiqueProvider = provider };
        var clients = new List<HttpClient>();
        var jobs = new List<(HttpClient Client, Guid Photo, Uri Status)>();
        var workers = new List<CritiqueWorker>();
        try
        {
            for (var owner = 0; owner < 3; owner++)
            {
                var subject = Guid.NewGuid().ToString();
                var client = await first.CreateAuthenticatedClientAsync(subject);
                clients.Add(client);
                for (var item = 0; item < 3; item++)
                {
                    var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
                    using var brief = await client.PutAsJsonAsync($"/api/photographs/{id}/brief", new { revision = 1, intent = subject });
                    brief.EnsureSuccessStatusCode();
                    using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{id}/critique")
                    { Content = JsonContent.Create(new { revision = 2 }) };
                    request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
                    using var response = await client.SendAsync(request);
                    Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
                    jobs.Add((client, id, response.Headers.Location!));
                }
            }
            for (var host = 0; host < hostCount; host++)
            {
                var services = (host == 0 ? first : second).Services;
                var worker = new CritiqueWorker(services.GetRequiredService<IServiceScopeFactory>(),
                    services.GetRequiredService<IOptions<AiOptions>>(), NullLogger<CritiqueWorker>.Instance);
                workers.Add(worker);
                await worker.StartAsync(default);
            }
            await filled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(300);
            lock (gate)
            {
                Assert.Equal(capacity, active);
                Assert.Equal(capacity, maximum);
                Assert.InRange(ownerMaximum, 1, 2);
            }
            release.TrySetResult();
            foreach (var (client, _, status) in jobs)
            {
                var completed = false;
                for (var attempt = 0; attempt < 100; attempt++)
                {
                    var operation = await client.GetFromJsonAsync<JsonElement>(status);
                    if (operation.GetProperty("status").GetString() == "Succeeded") { completed = true; break; }
                    await Task.Delay(100);
                }
                Assert.True(completed, "The worker did not publish the queued critique.");
            }
            Assert.Equal(9, provider.Calls);
            lock (gate) { Assert.Equal(0, active); Assert.Equal(capacity, maximum); Assert.InRange(ownerMaximum, 1, 2); }
        }
        finally
        {
            foreach (var worker in workers)
            {
                using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await worker.StopAsync(stopping.Token);
                worker.Dispose();
            }
            release.TrySetResult();
            foreach (var (client, photo, _) in jobs)
            {
                var latest = await client.GetFromJsonAsync<JsonElement>($"/api/photographs/{photo}");
                using var deleted = await client.DeleteAsync($"/api/photographs/{photo}?revision={latest.GetProperty("revision").GetInt64()}");
                deleted.EnsureSuccessStatusCode();
            }
            foreach (var client in clients) client.Dispose();
        }
    }
}
