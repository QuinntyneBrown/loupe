// Given completed equivalent analysis, when requested again, then the saved
// result is reused without provider work; explicit regeneration creates one job.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Application.Critiques;
using Loupe.Api.Tests.Photographs;
using Loupe.Domain.Critiques;
using Loupe.Domain.Operations;
using Loupe.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.Critiques;

public sealed class CritiqueReuseTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_035_1_Upgrading_existing_saved_results_preserves_completed_reuse()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot)
        { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only-key" } };
        var subject = Guid.NewGuid().ToString();
        using var client = await factory.CreateAuthenticatedClientAsync(subject);
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var original = await SubmitAsync(client, id);
        Assert.Equal(HttpStatusCode.Accepted, original.StatusCode);
        await RunAsync(factory);
        var saved = await client.GetStringAsync($"/api/photographs/{id}/critique");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync("20260907235700_CritiqueAttempts");
            await migrator.MigrateAsync();
            await factory.ReauthenticateAsync(client, subject);
        }
        using var repeated = await SubmitAsync(client, id);
        Assert.Equal(HttpStatusCode.Accepted, repeated.StatusCode);
        Assert.Equal(original.Headers.Location, repeated.Headers.Location);
        Assert.Equal(saved, await client.GetStringAsync($"/api/photographs/{id}/critique"));
    }

    [Fact]
    public async Task L2_008_5_035_1_Completed_inputs_reuse_saved_work_and_regeneration_replaces_only_on_success()
    {
        var provider = new ControlledCritiqueProvider((_, _, _) => Task.FromResult(CritiqueResultFixture.Valid()));
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot)
        { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only-key" }, CritiqueProvider = provider };
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        await SaveBriefAsync(client, id, "Original private brief");
        using var first = await SubmitAsync(client, id);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        await RunAsync(factory);
        var original = await client.GetStringAsync($"/api/photographs/{id}/critique");
        using var repeated = await SubmitAsync(client, id);
        Assert.Equal(HttpStatusCode.Accepted, repeated.StatusCode);
        Assert.Equal(first.Headers.Location, repeated.Headers.Location);
        Assert.Equal("Succeeded", (await repeated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(1, provider.Calls);
        using var notes = await client.PutAsJsonAsync($"/api/photographs/{id}/notes", new { revision = await RevisionAsync(client, id), notes = "Notes remain private" });
        notes.EnsureSuccessStatusCode();
        var notesRevision = await RevisionAsync(client, id);
        using var afterNotes = await SubmitAsync(client, id);
        Assert.Equal(first.Headers.Location, afterNotes.Headers.Location);
        Assert.Equal(notesRevision, await RevisionAsync(client, id));
        await SaveBriefAsync(client, id, "A different private brief");
        using var changed = await SubmitAsync(client, id);
        Assert.NotEqual(first.Headers.Location, changed.Headers.Location);
        Assert.Equal(original, await client.GetStringAsync($"/api/photographs/{id}/critique"));
        await RunAsync(factory);
        Assert.Equal(2, provider.Calls);
        await SaveBriefAsync(client, id, "Original private brief");
        using var restored = await SubmitAsync(client, id);
        Assert.True(restored.StatusCode == HttpStatusCode.Accepted,
            $"Restored critique request returned {restored.StatusCode}: {await restored.Content.ReadAsStringAsync()}\n{factory.Failure.Exception}");
        Assert.Equal(first.Headers.Location, restored.Headers.Location);
        Assert.Equal(original, await client.GetStringAsync($"/api/photographs/{id}/critique"));
        Assert.Equal(2, provider.Calls);
        using var regenerated = await SubmitAsync(client, id, regenerate: true);
        Assert.NotEqual(first.Headers.Location, regenerated.Headers.Location);
        using var activeRepeat = await SubmitAsync(client, id, regenerate: true);
        Assert.Equal(regenerated.Headers.Location, activeRepeat.Headers.Location);
        Assert.Equal(original, await client.GetStringAsync($"/api/photographs/{id}/critique"));
        await RunAsync(factory);
        Assert.Equal(3, provider.Calls);
        var result = await client.GetFromJsonAsync<JsonElement>($"/api/photographs/{id}/critique");
        Assert.Equal($"/api/operations/{result.GetProperty("operationId").GetGuid()}", regenerated.Headers.Location!.OriginalString);
        var queuedIds = new List<Guid>();
        for (var index = 0; index < 5; index++)
        {
            var queuedId = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
            queuedIds.Add(queuedId);
            using var queued = await SubmitAsync(client, queuedId);
            Assert.Equal(HttpStatusCode.Accepted, queued.StatusCode);
        }
        using var atCap = await SubmitAsync(client, id);
        Assert.Equal(HttpStatusCode.Accepted, atCap.StatusCode);
        Assert.Equal(regenerated.Headers.Location, atCap.Headers.Location);
        foreach (var queuedId in queuedIds)
        {
            using var cleanup = await client.DeleteAsync($"/api/photographs/{queuedId}?revision=1");
            cleanup.EnsureSuccessStatusCode();
        }
        using var deleted = await client.DeleteAsync($"/api/photographs/{id}?revision={await RevisionAsync(client, id)}");
        deleted.EnsureSuccessStatusCode();
        await using var scope = factory.Services.CreateAsyncScope();
        var retained = await scope.ServiceProvider.GetRequiredService<LibraryDbContext>().BackgroundOperations.AsNoTracking()
            .Where(operation => operation.ResourceId == id).ToListAsync();
        Assert.All(retained, operation => Assert.Null(operation.InputJson));
        var retainedJson = JsonSerializer.Serialize(retained);
        Assert.DoesNotContain("private brief", retainedJson);
        Assert.DoesNotContain("uniform black field", retainedJson);
    }

    [Fact]
    public async Task L2_035_5_Different_execution_mode_does_not_reuse_demo_output()
    {
        var subject = Guid.NewGuid().ToString();
        await using var original = new ApiFactory(database.ConnectionString, database.MediaRoot)
        { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only-key" } };
        using var client = await original.CreateAuthenticatedClientAsync(subject);
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var first = await SubmitAsync(client, id);
        await RunAsync(original);
        // Seed a historical sample without enabling retired product execution.
        await using (var scope = original.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            var photo = await context.Photographs.SingleAsync(item => item.Id == id);
            var sample = JsonSerializer.Deserialize<SavedCritique>(photo.CritiqueJson!)! with { Mode = ExecutionMode.Demo };
            photo.CritiqueJson = JsonSerializer.Serialize(sample);
            await context.BackgroundOperations.Where(item => item.Id == sample.OperationId).ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Mode, ExecutionMode.Demo)
                .SetProperty(item => item.OutputJson, photo.CritiqueJson));
            await context.SaveChangesAsync();
        }
        await using var live = new ApiFactory(database.ConnectionString, database.MediaRoot)
        { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only-no-external-calls" } };
        using var later = await live.CreateAuthenticatedClientAsync(subject);
        using var changed = await SubmitAsync(later, id);
        Assert.Equal(HttpStatusCode.Accepted, changed.StatusCode);
        Assert.NotEqual(first.Headers.Location, changed.Headers.Location);
        var operation = await changed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Live", operation.GetProperty("mode").GetString());
        Assert.Equal("Queued", operation.GetProperty("status").GetString());
        // Cancel this admitted work before another test uses the shared worker queue.
        using var cleanup = await later.DeleteAsync($"/api/photographs/{id}?revision={await RevisionAsync(later, id)}");
        cleanup.EnsureSuccessStatusCode();
    }

    // Acceptance Test. Traces to: L2-036.8, L2-035.1.
    [Fact]
    public async Task Azure_requests_preserve_but_do_not_reuse_direct_OpenAI_results()
    {
        var provider = new ControlledCritiqueProvider((_, _, _) => Task.FromResult(CritiqueResultFixture.Valid()));
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot)
        { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only-key" }, CritiqueProvider = provider };
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var first = await SubmitAsync(client, id);
        await RunAsync(factory);
        // Reproduce a persisted direct-OpenAI result without invoking an external service.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            var photo = await context.Photographs.SingleAsync(item => item.Id == id);
            var old = JsonSerializer.Deserialize<SavedCritique>(photo.CritiqueJson!)! with { PromptVersion = "critique-v2" };
            photo.CritiqueJson = JsonSerializer.Serialize(old);
            await context.BackgroundOperations.Where(item => item.Id == old.OperationId).ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.PromptVersion, "critique-v2")
                .SetProperty(item => item.OutputJson, photo.CritiqueJson));
            await context.SaveChangesAsync();
        }
        var previous = await client.GetStringAsync($"/api/photographs/{id}/critique");
        using var changed = await SubmitAsync(client, id);
        Assert.Equal(HttpStatusCode.Accepted, changed.StatusCode);
        Assert.NotEqual(first.Headers.Location, changed.Headers.Location);
        Assert.Equal("Queued", (await changed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(previous, await client.GetStringAsync($"/api/photographs/{id}/critique"));
        await RunAsync(factory);
        var replacement = await client.GetFromJsonAsync<JsonElement>($"/api/photographs/{id}/critique");
        Assert.Equal("azure-critique-v2", replacement.GetProperty("promptVersion").GetString());
        Assert.Equal("gpt-5.4-mini-2026-03-17", replacement.GetProperty("model").GetString());
        using var repeated = await SubmitAsync(client, id);
        Assert.Equal(changed.Headers.Location, repeated.Headers.Location);
        Assert.Equal(2, provider.Calls);
    }

    private static async Task<long> RevisionAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<JsonElement>($"/api/photographs/{id}")).GetProperty("revision").GetInt64();

    private static async Task SaveBriefAsync(HttpClient client, Guid id, string intent)
    {
        using var saved = await client.PutAsJsonAsync($"/api/photographs/{id}/brief", new { revision = await RevisionAsync(client, id), intent });
        saved.EnsureSuccessStatusCode();
    }

    private static async Task RunAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCritiqueCommand()));
    }

    private static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid id, bool regenerate = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{id}/critique")
        { Content = JsonContent.Create(new { revision = await RevisionAsync(client, id), regenerate }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }
}
