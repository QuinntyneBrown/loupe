// Given an owned reference, when its import status is reopened, then the last
// admitted operation is discoverable without a browser-held operation identifier.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Domain.Operations;
using Loupe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.References;

public sealed class CurrentReferenceImportTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private ApiFactory Factory() => new(database.ConnectionString, database.MediaRoot)
    { Settings = new Dictionary<string, string?> { ["Imports:Mode"] = "Demo" } };
    [Fact]
    public async Task L2_033_5_No_import_is_distinct_from_foreign_missing_and_anonymous_status()
    {
        await using var factory = Factory(); using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = await SaveAsync(owner);
        using var empty = await owner.GetAsync(Location(id)); Assert.Equal(HttpStatusCode.NoContent, empty.StatusCode);
        foreach (var target in new[] { id, Guid.NewGuid() })
        { using var hidden = await stranger.GetAsync(Location(target)); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode); Assert.Equal("item_unavailable", (await hidden.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()); }
        using var anonymous = factory.CreateClient(); using var denied = await anonymous.GetAsync(Location(id)); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }
    [Fact]
    public async Task L2_033_2_Admission_and_active_reuse_are_discoverable_after_restart()
    {
        var subject = Guid.NewGuid().ToString(); Guid id; string operation;
        await using (var factory = Factory())
        {
            using var client = await factory.CreateAuthenticatedClientAsync(subject); id = await SaveAsync(client);
            using var admitted = await SubmitAsync(client, id); Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
            operation = await admitted.Content.ReadAsStringAsync(); Assert.Equal(operation, await client.GetStringAsync(Location(id)));
            using var reused = await SubmitAsync(client, id); Assert.Equal(admitted.Headers.Location, reused.Headers.Location); Assert.Equal(operation, await client.GetStringAsync(Location(id)));
        }
        await using var restart = Factory(); using var later = await restart.CreateAuthenticatedClientAsync(subject);
        Assert.Equal(operation, await later.GetStringAsync(Location(id)));
    }
    [Fact]
    public async Task L2_033_2_Status_reads_follow_persisted_state_and_do_not_expose_snapshot_data()
    {
        await using var factory = Factory(); using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString()); var id = await SaveAsync(client);
        using var admitted = await SubmitAsync(client, id); Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        var operationId = (await admitted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        foreach (var state in new[] { OperationStatus.Running, OperationStatus.Failed, OperationStatus.Canceled, OperationStatus.Succeeded })
        {
            await using var scope = factory.Services.CreateAsyncScope(); var store = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            await store.BackgroundOperations.Where(item => item.Id == operationId).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, state));
            var current = await client.GetStringAsync(Location(id)); Assert.Equal(state.ToString(), JsonSerializer.Deserialize<JsonElement>(current).GetProperty("status").GetString());
            Assert.Equal(await client.GetStringAsync(admitted.Headers.Location), current); Assert.DoesNotContain("source.example", current); Assert.DoesNotContain("InputJson", current);
        }
    }
    [Fact]
    public async Task L2_030_033_New_admission_replaces_current_status_but_historical_receipt_replay_does_not()
    {
        await using var factory = Factory(); using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString()); var id = await SaveAsync(client);
        using var first = await SubmitAsync(client, id, "first"); Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await using (var scope = factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<LibraryDbContext>().BackgroundOperations.Where(item => item.Id == firstId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, OperationStatus.Failed));
        using var second = await SubmitAsync(client, id); Assert.Equal(HttpStatusCode.Accepted, second.StatusCode); Assert.NotEqual(first.Headers.Location, second.Headers.Location);
        using var replay = await SubmitAsync(client, id, "first"); Assert.Equal(first.Headers.Location, replay.Headers.Location);
        Assert.Equal(await second.Content.ReadAsStringAsync(), await client.GetStringAsync(Location(id)));
    }
    [Fact]
    public async Task L2_029_4_Existing_admission_remains_discoverable_after_status_schema_upgrade()
    {
        await using var factory = Factory(); using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString()); var id = await SaveAsync(client);
        using var admitted = await SubmitAsync(client, id); Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var migrator = scope.ServiceProvider.GetRequiredService<LibraryDbContext>().GetService<IMigrator>();
            await migrator.MigrateAsync("20260908061754_ReferenceSourceLookup"); await migrator.MigrateAsync();
        }
        Assert.Equal(await admitted.Content.ReadAsStringAsync(), await client.GetStringAsync(Location(id)));
    }
    private static string Location(Guid id) => $"/api/references/{id}/imports/operation";
    private static async Task<Guid> SaveAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/references/links") { Content = JsonContent.Create(new { sourceUrl = "https://source.example/photo" }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString()); using var response = await client.SendAsync(request); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reference").GetProperty("id").GetGuid();
    }
    private static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid id, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/references/{id}/imports") { Content = JsonContent.Create(new { revision = 1 }) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString()); return await client.SendAsync(request);
    }
}
