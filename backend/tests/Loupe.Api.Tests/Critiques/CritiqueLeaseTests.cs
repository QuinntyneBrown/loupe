// Given an API-admitted job whose worker stops renewing, when another worker
// recovers it after 60 seconds, then stale publication is fenced and recovery is bounded.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Application.Critiques;
using Loupe.Api.Tests.Photographs;
using Loupe.Domain.Operations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.Critiques;

public sealed class CritiqueLeaseTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task L2_033_3_035_6_Expired_worker_is_fenced_and_only_one_recovery_call_is_allowed(bool interruptedAgain)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot)
        { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only-key" } };
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{id}/critique")
        { Content = JsonContent.Create(new { revision = 1 }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var admitted = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        await using var firstScope = factory.Services.CreateAsyncScope();
        var firstWorker = firstScope.ServiceProvider.GetRequiredService<ICritiqueWorkStore>();
        var first = await firstWorker.ClaimAsync(ExecutionMode.Live, default);
        Assert.NotNull(first);
        var running = await client.GetFromJsonAsync<JsonElement>(admitted.Headers.Location);
        Assert.Equal("Running", running.GetProperty("status").GetString());
        factory.Clock.Advance(TimeSpan.FromSeconds(59));
        await using var secondScope = factory.Services.CreateAsyncScope();
        var secondWorker = secondScope.ServiceProvider.GetRequiredService<ICritiqueWorkStore>();
        Assert.Null(await secondWorker.ClaimAsync(ExecutionMode.Live, default));
        factory.Clock.Advance(TimeSpan.FromSeconds(2));
        var recovered = await secondWorker.ClaimAsync(ExecutionMode.Live, default);
        Assert.NotNull(recovered);
        Assert.Equal(first.Id, recovered.Id);
        Assert.NotEqual(first.LeaseToken, recovered.LeaseToken);
        await firstWorker.PublishAsync(first, CritiqueResultFixture.Valid(), default);
        await firstWorker.RejectInvalidAsync(first, default);
        var fenced = await client.GetFromJsonAsync<JsonElement>(admitted.Headers.Location);
        Assert.Equal("Running", fenced.GetProperty("status").GetString());
        using var empty = await client.GetAsync($"/api/photographs/{id}/critique");
        Assert.Equal(HttpStatusCode.NoContent, empty.StatusCode);
        if (interruptedAgain)
        {
            factory.Clock.Advance(TimeSpan.FromSeconds(61));
            await using var thirdScope = factory.Services.CreateAsyncScope();
            var thirdWorker = thirdScope.ServiceProvider.GetRequiredService<ICritiqueWorkStore>();
            Assert.Null(await thirdWorker.ClaimAsync(ExecutionMode.Live, default));
            var failed = await client.GetFromJsonAsync<JsonElement>(admitted.Headers.Location);
            Assert.Equal("Failed", failed.GetProperty("status").GetString());
            Assert.Equal("worker_interrupted", failed.GetProperty("failureCode").GetString());
            await secondWorker.PublishAsync(recovered, CritiqueResultFixture.Valid(), default);
            using var stillEmpty = await client.GetAsync($"/api/photographs/{id}/critique");
            Assert.Equal(HttpStatusCode.NoContent, stillEmpty.StatusCode);
        }
        else
        {
            await secondWorker.PublishAsync(recovered, CritiqueResultFixture.Valid(), default);
            var saved = await client.GetFromJsonAsync<JsonElement>($"/api/photographs/{id}/critique");
            Assert.Equal(first.Id, saved.GetProperty("operationId").GetGuid());
            var status = await client.GetFromJsonAsync<JsonElement>(admitted.Headers.Location);
            Assert.Equal("Succeeded", status.GetProperty("status").GetString());
        }
    }
}
