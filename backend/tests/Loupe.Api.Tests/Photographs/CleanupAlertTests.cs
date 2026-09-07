// Given blocked physical cleanup, when its age crosses 24 hours, then an operator
// receives a structured, correlated alert without private photographic content.
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Persistence;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class CleanupAlertTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture database = new();
    public Task InitializeAsync() => database.InitializeAsync();
    public Task DisposeAsync() => database.DisposeAsync();

    [Theory]
    [InlineData(23, false)]
    [InlineData(25, true)]
    public async Task L2_032_4_Overdue_cleanup_has_actionable_private_safe_diagnostics(int hours, bool overdue)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        factory.Clock.Advance(-TimeSpan.FromHours(hours));
        var before = Directory.Exists(database.MediaRoot) ? Directory.GetFiles(database.MediaRoot) : [];
        const string privateTitle = "Private portrait of Ari at home";
        var photo = await PhotographFixture.UploadAsync(client, privateTitle);
        var files = Directory.GetFiles(database.MediaRoot).Except(before).ToArray();
        var blocked = files[0];
        Assert.StartsWith(Path.GetFullPath(database.MediaRoot) + Path.DirectorySeparatorChar, Path.GetFullPath(blocked));
        File.Move(blocked, blocked + ".fixture");
        Directory.CreateDirectory(blocked);
        try
        {
            using var deleted = await client.DeleteAsync($"/api/photographs/{photo.GetProperty("id").GetGuid()}?revision=1");
            deleted.EnsureSuccessStatusCode();
            var deletionId = (await deleted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            await using var worker = new CleanupProcess(database.ConnectionString, database.MediaRoot);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            JsonElement diagnostic;
            while (true)
            {
                await worker.EnsureRunningAsync();
                var events = ReadEvents(worker.Lines);
                diagnostic = events.FirstOrDefault(entry => entry.GetProperty("EventId").GetInt32() == (overdue ? 3201 : 3202));
                if (diagnostic.ValueKind != JsonValueKind.Undefined) break;
                await Task.Delay(50, deadline.Token);
            }
            var scope = diagnostic.GetProperty("Scopes").EnumerateArray().Last(value => value.TryGetProperty("EntryPoint", out var point)
                && point.GetString() == "cleanup_worker");
            Assert.True(Guid.TryParseExact(scope.GetProperty("RunId").GetString(), "N", out _));
            Assert.Equal(overdue ? "Error" : "Warning", diagnostic.GetProperty("LogLevel").GetString());
            if (overdue)
            {
                var state = diagnostic.GetProperty("State");
                Assert.Equal("cleanup_overdue", state.GetProperty("EventName").GetString());
                Assert.True(state.GetProperty("PendingCount").GetInt32() >= 1);
                Assert.True(state.GetProperty("OldestAgeSeconds").GetDouble() >= 86400);
                Assert.Equal("docs/operations/cleanup.md", state.GetProperty("Runbook").GetString());
            }
            var status = await client.GetFromJsonAsync<JsonElement>($"/api/deletions/{deletionId}");
            Assert.Equal("Pending", status.GetProperty("status").GetString());
            var logs = await worker.StopAndReadLogsAsync();
            if (!overdue) Assert.DoesNotContain(ReadEvents(worker.Lines), entry => entry.GetProperty("EventId").GetInt32() == 3201);
            Assert.DoesNotContain(privateTitle, logs);
            Assert.DoesNotContain(Path.GetFileName(blocked), logs);
            Assert.DoesNotContain(database.ConnectionString, logs);
        }
        finally
        {
            if (Directory.Exists(blocked)) Directory.Delete(blocked);
            if (File.Exists(blocked + ".fixture")) File.Move(blocked + ".fixture", blocked, overwrite: true);
        }
    }

    private static JsonElement[] ReadEvents(string[] lines) => lines.Where(line => line.StartsWith('{'))
        .Select(line => { using var parsed = JsonDocument.Parse(line); return parsed.RootElement.Clone(); }).ToArray();
}
