// Given queued work for several owners, when workers compete for it, then owners
// rotate fairly and shared active leases never exceed four overall or two per owner.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Photographs;
using Loupe.Application.Critiques;
using Loupe.Application.Operations;
using Loupe.Domain.Operations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.Critiques;

public sealed class CritiqueSchedulingTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    public async Task L2_035_4_Workers_share_capacity_and_rotate_eligible_owners(int owners, bool concurrent)
    {
        await using var first = new ApiFactory(database.ConnectionString, database.MediaRoot)
        { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only-key" } };
        await using var second = new ApiFactory(database.ConnectionString, database.MediaRoot)
        { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only-key" } };
        var photographs = new List<(HttpClient Client, Guid Id)>();
        var clients = new List<HttpClient>();
        try
        {
            for (var owner = 0; owner < owners; owner++)
            {
                var client = await first.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
                clients.Add(client);
                for (var item = 0; item < 3; item++)
                {
                    var photo = await PhotographFixture.UploadAsync(client);
                    var id = photo.GetProperty("id").GetGuid();
                    photographs.Add((client, id));
                    using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{id}/critique")
                    { Content = JsonContent.Create(new { revision = 1 }) };
                    request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
                    using var response = await client.SendAsync(request);
                    Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
                }
            }

            async Task<BackgroundOperation?> Claim(int index)
            {
                await using var scope = (index % 2 == 0 ? first : second).Services.CreateAsyncScope();
                return await scope.ServiceProvider.GetRequiredService<ICritiqueWorkStore>().ClaimAsync(ExecutionMode.Live, default);
            }

            BackgroundOperation?[] attempts;
            if (concurrent) attempts = await Task.WhenAll(Enumerable.Range(0, 8).Select(Claim));
            else
            {
                var ordered = new List<BackgroundOperation?>();
                for (var index = 0; index < 8; index++) ordered.Add(await Claim(index));
                attempts = ordered.ToArray();
            }
            var running = attempts.OfType<BackgroundOperation>().ToArray();
            Assert.Equal(owners == 1 ? 2 : 4, running.Length);
            Assert.All(running.GroupBy(operation => operation.OwnerId), group => Assert.InRange(group.Count(), 1, 2));
            if (!concurrent && owners == 3)
            {
                Assert.Equal(3, running.Take(3).Select(operation => operation.OwnerId).Distinct().Count());
                Assert.Equal(running[0].OwnerId, running[3].OwnerId);
            }
            foreach (var operation in running)
            {
                var client = photographs.Single(photo => photo.Id == operation.ResourceId).Client;
                var status = await client.GetFromJsonAsync<JsonElement>($"/api/operations/{operation.Id}");
                Assert.Equal("Running", status.GetProperty("status").GetString());
            }
            await using var finishing = first.Services.CreateAsyncScope();
            await finishing.ServiceProvider.GetRequiredService<ICritiqueWorkStore>().RejectProviderAsync(running[0],
                new ProviderFailureException(ProviderFailureKind.Disabled), default);
            var next = await Claim(0);
            Assert.NotNull(next);
            if (!concurrent && owners == 3) Assert.Equal(running[1].OwnerId, next.OwnerId);
            Assert.Null(await Claim(1));
        }
        finally
        {
            foreach (var (client, id) in photographs)
            {
                using var deleted = await client.DeleteAsync($"/api/photographs/{id}?revision=1");
                Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
            }
            foreach (var client in clients) client.Dispose();
        }
    }
}
