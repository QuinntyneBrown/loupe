// Given an admitted Live critique, when an independent worker host runs, then a
// complete result with the original brief is saved durably without changing notes.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Photographs;
using Loupe.Infrastructure.Ai;
using Loupe.Infrastructure.Persistence;
using Loupe.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loupe.Api.Tests.Critiques;

public sealed class CritiqueExecutionTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_006_1_008_4_036_1_Independent_worker_publishes_complete_critique_with_immutable_brief()
    {
        var subject = Guid.NewGuid().ToString();
        Guid id;
        string saved;
        await using (var factory = new ApiFactory(database.ConnectionString, database.MediaRoot)
        { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only-key" } })
        {
            using var client = await factory.CreateAuthenticatedClientAsync(subject);
            id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
            using var brief = await client.PutAsJsonAsync($"/api/photographs/{id}/brief", new { revision = 1, intent = "Deliberate motion blur", requestedFeedback = "Keep the sense of movement" });
            brief.EnsureSuccessStatusCode();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{id}/critique")
            { Content = JsonContent.Create(new { revision = 2 }) };
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            using var admitted = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
            using var edit = await client.PutAsJsonAsync($"/api/photographs/{id}/brief", new { revision = 2, intent = "A later intent" });
            edit.EnsureSuccessStatusCode();
            using var notes = await client.PutAsJsonAsync($"/api/photographs/{id}/notes", new { revision = 3, notes = "Private journal" });
            notes.EnsureSuccessStatusCode();
            // A separate service provider discovers the durable admission; only its
            // external analysis boundary is replaced with a controlled test provider.
            await using var workerHost = new ApiFactory(database.ConnectionString, database.MediaRoot)
            { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only-key" } };
            using var worker = new AnalysisWorker(workerHost.Services.GetRequiredService<IServiceScopeFactory>(),
                workerHost.Services.GetRequiredService<IOptions<AiOptions>>(), NullLogger<AnalysisWorker>.Instance);
            JsonElement status = default;
            await worker.StartAsync(default);
            try
            {
                var deadline = DateTime.UtcNow.AddSeconds(15);
                do
                {
                    status = await client.GetFromJsonAsync<JsonElement>(admitted.Headers.Location);
                    if (status.GetProperty("status").GetString() == "Succeeded") break;
                    await Task.Delay(100);
                } while (DateTime.UtcNow < deadline);
            }
            finally { await worker.StopAsync(default); }
            Assert.Equal("Succeeded", status.GetProperty("status").GetString());
            Assert.NotEqual(JsonValueKind.Null, status.GetProperty("completedAt").ValueKind);
            using var result = await client.GetAsync($"/api/photographs/{id}/critique");
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            saved = await result.Content.ReadAsStringAsync();
            var critique = JsonSerializer.Deserialize<JsonElement>(saved);
            Assert.Equal("Live", critique.GetProperty("mode").GetString());
            Assert.Equal("Deliberate motion blur", critique.GetProperty("brief").GetProperty("intent").GetString());
            Assert.Equal(status.GetProperty("id").GetGuid(), critique.GetProperty("operationId").GetGuid());
            Assert.True(critique.GetProperty("generatedAt").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddMinutes(-1));
            var content = critique.GetProperty("content");
            Assert.NotEmpty(content.GetProperty("strengths").EnumerateArray());
            foreach (var aspect in new[] { "exposure", "focus", "depthOfField", "motion", "lighting", "color", "processing", "framing", "subjectSeparation", "balance", "visualHierarchy", "mood" })
            {
                var observation = content.GetProperty(aspect);
                Assert.False(string.IsNullOrWhiteSpace(observation.GetProperty("explanation").GetString()));
                if (!observation.GetProperty("assessable").GetBoolean())
                    Assert.False(string.IsNullOrWhiteSpace(observation.GetProperty("uncertaintyReason").GetString()));
            }
            var priorities = content.GetProperty("improvements").EnumerateArray().ToArray();
            Assert.Equal(3, priorities.Length);
            foreach (var priority in priorities)
                foreach (var field in new[] { "observation", "effect", "action" }) Assert.False(string.IsNullOrWhiteSpace(priority.GetProperty(field).GetString()));
            foreach (var field in new[] { "action", "comparison" })
                Assert.False(string.IsNullOrWhiteSpace(content.GetProperty("exercise").GetProperty(field).GetString()));
            Assert.DoesNotContain("Private journal", saved);
            Assert.DoesNotContain("imageKey", saved, StringComparison.OrdinalIgnoreCase);
            var photo = await client.GetFromJsonAsync<JsonElement>($"/api/photographs/{id}");
            Assert.Equal("Private journal", photo.GetProperty("notes").GetString());
            Assert.Equal("A later intent", photo.GetProperty("brief").GetProperty("intent").GetString());
        }
        await using var restarted = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var later = await restarted.CreateAuthenticatedClientAsync(subject);
        using var durable = await later.GetAsync($"/api/photographs/{id}/critique");
        Assert.Equal(saved, await durable.Content.ReadAsStringAsync());
        using var stranger = await restarted.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var hidden = await stranger.GetAsync($"/api/photographs/{id}/critique");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        var current = await later.GetFromJsonAsync<JsonElement>($"/api/photographs/{id}");
        using var deleted = await later.DeleteAsync($"/api/photographs/{id}?revision={current.GetProperty("revision").GetInt64()}");
        deleted.EnsureSuccessStatusCode();
        using var removed = await later.GetAsync($"/api/photographs/{id}/critique");
        Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
        // Inspect retained data, not code structure: deleting content must erase
        // its private submitted brief and image reference from completed jobs too.
        var operationId = JsonSerializer.Deserialize<JsonElement>(saved).GetProperty("operationId").GetGuid();
        await using var scope = restarted.Services.CreateAsyncScope();
        var databaseContext = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var retained = await databaseContext.BackgroundOperations.AsNoTracking().SingleAsync(operation => operation.Id == operationId);
        Assert.Null(retained.InputJson);
    }
}
