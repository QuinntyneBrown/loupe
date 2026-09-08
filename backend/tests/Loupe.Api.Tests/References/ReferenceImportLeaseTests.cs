// Given API-admitted work of multiple types, when workers claim and renew it,
// then owner fairness, deployment capacity and recoverable ownership are shared.
using Loupe.Api.Tests.Security;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Photographs;
using Loupe.Application.Operations;
using Loupe.Domain.Operations;
using Loupe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.References;

public sealed class ReferenceImportLeaseTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private static readonly OperationCapability[] Both = [new(OperationType.Critique, ExecutionMode.Demo), new(OperationType.ReferenceImport, ExecutionMode.Demo)];
    private ApiFactory Factory(TestClock clock, string imports = "Demo") => new(database.ConnectionString, database.MediaRoot)
    { ClockOverride = clock, Settings = new Dictionary<string, string?> { ["Imports:Mode"] = imports, ["Ai:Mode"] = "Demo" } };

    [Fact]
    public async Task L2_033_Import_claim_is_visible_and_renewal_prevents_premature_recovery()
    {
        var clock = new TestClock(); await using var first = Factory(clock); await using var second = Factory(clock); var ids = new List<Guid>();
        try
        {
            using var client = await first.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString()); ids.Add(await AdmitAsync(client, false));
            await using var scope = first.Services.CreateAsyncScope(); var worker = scope.ServiceProvider.GetRequiredService<IOperationLeaseStore>();
            var lease = await worker.ClaimAsync(Both, default); Assert.NotNull(lease); Assert.Equal(ids[0], lease.Id); Assert.Equal(OperationType.ReferenceImport, lease.Type);
            var running = await client.GetFromJsonAsync<JsonElement>($"/api/operations/{lease.Id}"); Assert.Equal("Running", running.GetProperty("status").GetString());
            clock.Advance(TimeSpan.FromSeconds(50)); Assert.True(await worker.RenewAsync(lease, default));
            clock.Advance(TimeSpan.FromSeconds(20)); Assert.Null(await ClaimAsync(second, Both));
            clock.Advance(TimeSpan.FromSeconds(41)); var recovered = await ClaimAsync(second, Both); Assert.NotNull(recovered); Assert.Equal(lease.Id, recovered.Id); Assert.NotEqual(lease.LeaseToken, recovered.LeaseToken);
            Assert.False(await worker.RenewAsync(lease, default));
        }
        finally { await CleanupAsync(first, ids); }
    }

    [Fact]
    public async Task L2_033_035_A_second_interruption_finishes_the_import_with_a_safe_failure()
    {
        var clock = new TestClock(); await using var factory = Factory(clock); var ids = new List<Guid>();
        try
        {
            using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString()); ids.Add(await AdmitAsync(client, false));
            Assert.NotNull(await ClaimAsync(factory, Both)); clock.Advance(TimeSpan.FromSeconds(61)); Assert.NotNull(await ClaimAsync(factory, Both));
            clock.Advance(TimeSpan.FromSeconds(61)); Assert.Null(await ClaimAsync(factory, Both));
            var failed = await client.GetFromJsonAsync<JsonElement>($"/api/operations/{ids[0]}"); Assert.Equal("Failed", failed.GetProperty("status").GetString()); Assert.Equal("worker_interrupted", failed.GetProperty("failureCode").GetString());
        }
        finally { await CleanupAsync(factory, ids); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task L2_035_4_Mixed_types_share_four_global_two_owner_slots_and_fair_dispatch(bool concurrent)
    {
        var clock = new TestClock(); await using var first = Factory(clock); await using var second = Factory(clock); var ids = new List<Guid>();
        try
        {
            for (var owner = 0; owner < 3; owner++)
            {
                using var client = await first.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
                for (var item = 0; item < 3; item++) { ids.Add(await AdmitAsync(client, item == 1)); clock.Advance(TimeSpan.FromSeconds(1)); }
            }
            BackgroundOperation?[] attempts;
            if (concurrent) attempts = await Task.WhenAll(Enumerable.Range(0, 8).Select(index => ClaimAsync(index % 2 == 0 ? first : second, Both)));
            else { var ordered = new List<BackgroundOperation?>(); for (var i = 0; i < 8; i++) ordered.Add(await ClaimAsync(first, Both)); attempts = ordered.ToArray(); }
            var claimed = attempts.OfType<BackgroundOperation>().ToArray(); Assert.Equal(4, claimed.Length); Assert.Equal(4, claimed.Select(item => item.Id).Distinct().Count());
            Assert.All(claimed.GroupBy(item => item.OwnerId), group => Assert.InRange(group.Count(), 1, 2));
            Assert.Contains(claimed, item => item.Type == OperationType.ReferenceImport); Assert.Contains(claimed, item => item.Type == OperationType.Critique);
            if (!concurrent) { Assert.Equal(3, claimed.Take(3).Select(item => item.OwnerId).Distinct().Count()); Assert.Equal(claimed[0].OwnerId, claimed[3].OwnerId); }
        }
        finally { await CleanupAsync(first, ids); }
    }

    [Fact]
    public async Task L2_036_Only_exact_configured_type_and_mode_pairs_are_claimed()
    {
        var clock = new TestClock(); await using var factory = Factory(clock, "Live"); var ids = new List<Guid>();
        try
        {
            using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString()); ids.Add(await AdmitAsync(client, false)); ids.Add(await AdmitAsync(client, true));
            Assert.Null(await ClaimAsync(factory, [new(OperationType.Critique, ExecutionMode.Live), new(OperationType.ReferenceImport, ExecutionMode.Demo)]));
            var import = await ClaimAsync(factory, [new(OperationType.ReferenceImport, ExecutionMode.Live)]); Assert.NotNull(import); Assert.Equal(ids[0], import.Id);
            var critique = await ClaimAsync(factory, [new(OperationType.Critique, ExecutionMode.Demo)]); Assert.NotNull(critique); Assert.Equal(ids[1], critique.Id);
        }
        finally { await CleanupAsync(factory, ids); }
    }

    private static async Task<BackgroundOperation?> ClaimAsync(ApiFactory factory, OperationCapability[] capabilities)
    {
        await using var scope = factory.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<IOperationLeaseStore>().ClaimAsync(capabilities, default);
    }
    private static async Task CleanupAsync(ApiFactory factory, List<Guid> ids)
    {
        await using var scope = factory.Services.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<LibraryDbContext>().BackgroundOperations.Where(item => ids.Contains(item.Id))
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, OperationStatus.Canceled));
    }
    private static async Task<Guid> AdmitAsync(HttpClient client, bool critique)
    {
        Guid resourceId;
        if (critique) resourceId = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        else
        {
            using var save = new HttpRequestMessage(HttpMethod.Post, "/api/references/links") { Content = JsonContent.Create(new { sourceUrl = $"https://source.example/{Guid.NewGuid()}" }) };
            save.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString()); using var saved = await client.SendAsync(save); saved.EnsureSuccessStatusCode();
            resourceId = (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reference").GetProperty("id").GetGuid();
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, critique ? $"/api/photographs/{resourceId}/critique" : $"/api/references/{resourceId}/imports") { Content = JsonContent.Create(new { revision = 1 }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString()); using var response = await client.SendAsync(request); Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
}
