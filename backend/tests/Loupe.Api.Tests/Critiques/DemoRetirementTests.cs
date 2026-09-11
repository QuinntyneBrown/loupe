// Acceptance Test: L2-036. Historical samples are archived without losing user
// content, and retired execution configuration cannot enable simulated work.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Loupe.Api.Tests.Photographs;
using Loupe.Application.Critiques;
using Loupe.Domain.Operations;
using Loupe.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loupe.Api.Tests.Critiques;

public sealed class DemoRetirementTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData("Ai:Mode")]
    [InlineData("Imports:Mode")]
    public async Task L2_036_Retired_demo_configuration_is_rejected_at_startup(string setting)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot)
        { Settings = new Dictionary<string, string?> { [setting] = "Demo", ["Ai:ApiKey"] = "fixture-only-key" } };
        Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    }

    [Theory]
    [InlineData("Queued")]
    [InlineData("Running")]
    public async Task L2_036_Upgrade_archives_sample_and_releases_pending_work_for_real_critique(string pendingStatus)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot)
        { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only-key" } };
        var subject = Guid.NewGuid().ToString();
        using var client = await factory.CreateAuthenticatedClientAsync(subject);
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var notes = await client.PutAsJsonAsync($"/api/photographs/{id}/notes", new { revision = 1, notes = "Keep my personal journal." });
        notes.EnsureSuccessStatusCode();
        using var brief = await client.PutAsJsonAsync($"/api/photographs/{id}/brief", new { revision = 2, intent = "Keep the soft morning light.", requestedFeedback = "Simplify the background." });
        brief.EnsureSuccessStatusCode();
        var originalImage = await client.GetByteArrayAsync($"/api/photographs/{id}/image");
        using var first = await RequestAsync(client, id);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var completedId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await RunAsync(factory);
        using var regeneration = await RequestAsync(client, id, regenerate: true);
        Assert.Equal(HttpStatusCode.Accepted, regeneration.StatusCode);
        var pendingId = (await regeneration.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        string archived;
        BackgroundOperation oldLease;
        var leaseToken = Guid.NewGuid();
        var later = factory.Clock.GetUtcNow().AddMinutes(5);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            var pending = await context.BackgroundOperations.AsNoTracking().SingleAsync(item => item.Id == pendingId);
            oldLease = new BackgroundOperation
            {
                Id = pending.Id,
                OwnerId = pending.OwnerId,
                ResourceId = pending.ResourceId,
                Type = pending.Type,
                Mode = ExecutionMode.Demo,
                Model = pending.Model,
                PromptVersion = pending.PromptVersion,
                InputJson = pending.InputJson,
                CreatedAt = pending.CreatedAt,
                UpdatedAt = pending.UpdatedAt,
                Status = OperationStatus.Running,
                LeaseToken = leaseToken,
                LeaseExpiresAt = later
            };
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync("20260908065341_CurrentReferenceImport");
            try
            {
                // SQL deliberately uses only the old schema while it is downgraded.
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE photographs SET "CritiqueJson" = jsonb_set("CritiqueJson", ARRAY['Mode'], '0'::jsonb),
                        "CurrentCritiqueOperationId" = {pendingId} WHERE "Id" = {id}
                    """);
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE background_operations SET "Mode" = 'Demo',
                        "OutputJson" = CASE WHEN "OutputJson" IS NULL THEN NULL ELSE jsonb_set("OutputJson", ARRAY['Mode'], '0'::jsonb) END
                    WHERE "Id" = {completedId} OR "Id" = {pendingId}
                    """);
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE background_operations SET "Status" = {pendingStatus}, "LeaseToken" = {leaseToken},
                        "LeaseExpiresAt" = {later}, "NextAttemptAt" = {later}, "RetryAvailableAt" = {later}
                    WHERE "Id" = {pendingId}
                    """);
                archived = await context.Database.SqlQuery<string>($"""
                    SELECT "CritiqueJson"::text AS "Value" FROM photographs WHERE "Id" = {id}
                    """).SingleAsync();
            }
            finally { await migrator.MigrateAsync(); }
        }

        await factory.ReauthenticateAsync(client, subject);
        using var critique = await client.GetAsync($"/api/photographs/{id}/critique");
        Assert.Equal(HttpStatusCode.NoContent, critique.StatusCode);
        using var currentOperation = await client.GetAsync($"/api/photographs/{id}/critique/operation");
        Assert.Equal(HttpStatusCode.NoContent, currentOperation.StatusCode);
        var candidates = await client.GetFromJsonAsync<JsonElement>("/api/comparisons/eligible");
        Assert.DoesNotContain(candidates.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);
        var photo = await client.GetFromJsonAsync<JsonElement>($"/api/photographs/{id}");
        Assert.True(photo.GetProperty("hasArchivedDemoCritique").GetBoolean());
        Assert.Equal("Keep my personal journal.", photo.GetProperty("notes").GetString());
        Assert.Equal("Keep the soft morning light.", photo.GetProperty("brief").GetProperty("intent").GetString());
        Assert.Equal("Simplify the background.", photo.GetProperty("brief").GetProperty("requestedFeedback").GetString());
        Assert.Equal(originalImage, await client.GetByteArrayAsync($"/api/photographs/{id}/image"));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            var canceled = await context.BackgroundOperations.AsNoTracking().SingleAsync(item => item.Id == pendingId);
            Assert.Equal(OperationStatus.Canceled, canceled.Status);
            Assert.NotNull(canceled.CompletedAt);
            Assert.Null(canceled.LeaseToken);
            Assert.Null(canceled.LeaseExpiresAt);
            Assert.Null(canceled.NextAttemptAt);
            Assert.Null(canceled.RetryAvailableAt);
            Assert.False(await context.BackgroundOperations.AnyAsync(item => item.ResourceId == id && item.Mode == ExecutionMode.Live
                && (item.Status == OperationStatus.Queued || item.Status == OperationStatus.Running)));
            Assert.Equal(archived, await ReadArchiveAsync(context, id));
            // A worker holding the pre-upgrade lease cannot restore a sample after migration.
            var worker = scope.ServiceProvider.GetRequiredService<ICritiqueWorkStore>();
            Assert.False(await worker.RenewAsync(oldLease, default));
            await worker.PublishAsync(oldLease, CritiqueResultFixture.Valid(), default);
            using var fenced = await client.GetAsync($"/api/photographs/{id}/critique");
            Assert.Equal(HttpStatusCode.NoContent, fenced.StatusCode);
        }

        // A replacement requires an explicit real request and keeps the historical archive.
        using var replacement = await RequestAsync(client, id);
        Assert.Equal(HttpStatusCode.Accepted, replacement.StatusCode);
        Assert.NotEqual(first.Headers.Location, replacement.Headers.Location);
        Assert.NotEqual(regeneration.Headers.Location, replacement.Headers.Location);
        await RunAsync(factory);
        var live = await client.GetFromJsonAsync<JsonElement>($"/api/photographs/{id}/critique");
        Assert.Equal("Live", live.GetProperty("mode").GetString());
        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(archived), JsonNode.Parse(await ReadArchiveAsync(scope.ServiceProvider.GetRequiredService<LibraryDbContext>(), id))));
    }

    private static Task<string> ReadArchiveAsync(LibraryDbContext context, Guid id) => context.Database.SqlQuery<string>($"""
        SELECT "ArchivedDemoCritiqueJson"::text AS "Value" FROM photographs WHERE "Id" = {id}
        """).SingleAsync();

    private static async Task RunAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCritiqueCommand()));
    }

    private static async Task<HttpResponseMessage> RequestAsync(HttpClient client, Guid id, bool regenerate = false)
    {
        var photo = await client.GetFromJsonAsync<JsonElement>($"/api/photographs/{id}");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{id}/critique")
        { Content = JsonContent.Create(new { revision = photo.GetProperty("revision").GetInt64(), regenerate }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }
}
