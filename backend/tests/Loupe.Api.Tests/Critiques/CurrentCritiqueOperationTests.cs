// Given a saved photograph, when its detail is revisited, then the most recently
// admitted critique operation is recoverable without retaining a client-side id.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Photographs;
using Loupe.Application.Critiques;
using Loupe.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.Critiques;

public sealed class CurrentCritiqueOperationTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_003_2_033_5_Empty_owned_photographs_are_distinct_from_unavailable_resources()
    {
        await using var factory = CreateFactory();
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(owner)).GetProperty("id").GetGuid();
        using var empty = await owner.GetAsync(Location(id));
        Assert.Equal(HttpStatusCode.NoContent, empty.StatusCode);
        foreach (var target in new[] { id, Guid.NewGuid() })
        {
            using var hidden = await stranger.GetAsync(Location(target));
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
            Assert.Equal("item_unavailable", (await hidden.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
        using var admitted = await SubmitAsync(owner, id);
        Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        using var deleted = await owner.DeleteAsync($"/api/photographs/{id}?revision=1");
        deleted.EnsureSuccessStatusCode();
        using var unavailable = await owner.GetAsync(Location(id));
        Assert.Equal(HttpStatusCode.NotFound, unavailable.StatusCode);
    }

    [Fact]
    public async Task L2_033_2_Queued_running_and_completed_states_survive_revisit_and_restart()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new ControlledCritiqueProvider(async (_, _, token) =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(token);
            return CritiqueResultFixture.Valid();
        });
        var subject = Guid.NewGuid().ToString();
        await using var factory = CreateFactory(provider);
        using var client = await factory.CreateAuthenticatedClientAsync(subject);
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var admitted = await SubmitAsync(client, id);
        Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        Assert.Equal(await admitted.Content.ReadAsStringAsync(), await client.GetStringAsync(Location(id)));
        await using var scope = factory.Services.CreateAsyncScope();
        var run = scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCritiqueCommand());
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var running = await client.GetFromJsonAsync<JsonElement>(Location(id));
            Assert.Equal("Running", running.GetProperty("status").GetString());
            Assert.Equal(await client.GetStringAsync(admitted.Headers.Location), running.GetRawText());
        }
        finally { release.TrySetResult(); }
        Assert.True(await run);
        await using var restarted = CreateFactory();
        using var later = await restarted.CreateAuthenticatedClientAsync(subject);
        var completed = await later.GetFromJsonAsync<JsonElement>(Location(id));
        Assert.Equal("Succeeded", completed.GetProperty("status").GetString());
        Assert.Equal(await later.GetStringAsync(admitted.Headers.Location), completed.GetRawText());
    }

    [Fact]
    public async Task L2_008_5_033_2_Explicit_regeneration_and_returning_to_cached_inputs_update_the_current_operation()
    {
        await using var factory = CreateFactory();
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var first = await SubmitAsync(client, id);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        await RunAsync(factory);
        using var edited = await client.PutAsJsonAsync($"/api/photographs/{id}/brief", new { revision = await RevisionAsync(client, id), intent = "New intent" });
        edited.EnsureSuccessStatusCode();
        using var changed = await SubmitAsync(client, id);
        Assert.Equal(await changed.Content.ReadAsStringAsync(), await client.GetStringAsync(Location(id)));
        await RunAsync(factory);
        using var restoredBrief = await client.PutAsJsonAsync($"/api/photographs/{id}/brief", new { revision = await RevisionAsync(client, id) });
        restoredBrief.EnsureSuccessStatusCode();
        using var cached = await SubmitAsync(client, id);
        Assert.Equal(first.Headers.Location, cached.Headers.Location);
        Assert.Equal(await cached.Content.ReadAsStringAsync(), await client.GetStringAsync(Location(id)));
        using var regenerated = await SubmitAsync(client, id, true);
        Assert.NotEqual(first.Headers.Location, regenerated.Headers.Location);
        Assert.Equal(await regenerated.Content.ReadAsStringAsync(), await client.GetStringAsync(Location(id)));
        using var active = await SubmitAsync(client, id, true);
        Assert.Equal(regenerated.Headers.Location, active.Headers.Location);
        Assert.Equal(await active.Content.ReadAsStringAsync(), await client.GetStringAsync(Location(id)));
        using var deleted = await client.DeleteAsync($"/api/photographs/{id}?revision={await RevisionAsync(client, id)}");
        deleted.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task L2_033_2_Existing_admitted_work_is_discoverable_after_schema_upgrade()
    {
        await using var factory = CreateFactory();
        var subject = Guid.NewGuid().ToString();
        using var client = await factory.CreateAuthenticatedClientAsync(subject);
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var admitted = await SubmitAsync(client, id);
        Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var migrator = scope.ServiceProvider.GetRequiredService<LibraryDbContext>().GetService<IMigrator>();
            await migrator.MigrateAsync("20260908011430_CritiqueAnalysisImages");
            await migrator.MigrateAsync();
        }
        // The authentication migration intentionally revokes every previous session.
        using var expired = await client.GetAsync(Location(id));
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        using var later = await factory.CreateAuthenticatedClientAsync(subject);
        Assert.Equal(await admitted.Content.ReadAsStringAsync(), await later.GetStringAsync(Location(id)));
        using var deleted = await later.DeleteAsync($"/api/photographs/{id}?revision=1");
        deleted.EnsureSuccessStatusCode();
    }

    private ApiFactory CreateFactory(ICritiqueProvider? provider = null) => new(database.ConnectionString, database.MediaRoot)
    { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only-key" }, CritiqueProvider = provider };

    private static string Location(Guid id) => $"/api/photographs/{id}/critique/operation";

    private static async Task<long> RevisionAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<JsonElement>($"/api/photographs/{id}")).GetProperty("revision").GetInt64();

    private static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid id, bool regenerate = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{id}/critique")
        { Content = JsonContent.Create(new { revision = await RevisionAsync(client, id), regenerate }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    private static async Task RunAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCritiqueCommand()));
    }
}
