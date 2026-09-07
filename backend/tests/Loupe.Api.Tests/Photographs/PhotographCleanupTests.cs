// Given a durable deletion accepted by the API, when the independent worker runs,
// then physical cleanup completes without deleting another photograph's media.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Persistence;
using NetVips;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class PhotographCleanupTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_031_2_5_032_1_Worker_removes_deleted_media_and_reports_durable_completion()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        var subject = Guid.NewGuid().ToString();
        using var client = await factory.CreateAuthenticatedClientAsync(subject);
        var before = Directory.Exists(database.MediaRoot) ? Directory.GetFiles(database.MediaRoot).Length : 0;
        var removed = await PhotographFixture.UploadAsync(client, "Delete this study");
        var kept = await PhotographFixture.UploadAsync(client, "Keep this study");
        var id = removed.GetProperty("id").GetGuid();
        using var deleted = await client.DeleteAsync($"/api/photographs/{id}?revision=1");
        deleted.EnsureSuccessStatusCode();
        var operation = await deleted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", operation.GetProperty("status").GetString());
        JsonElement completed;
        await using (var worker = new CleanupProcess(database.ConnectionString, database.MediaRoot))
        await using (var secondWorker = new CleanupProcess(database.ConnectionString, database.MediaRoot))
        {
            completed = await WaitForCompletionAsync(client, operation.GetProperty("id").GetGuid(), worker);
            Assert.NotEqual(JsonValueKind.Null, completed.GetProperty("completedAt").ValueKind);
            Assert.Equal(before + 2, Directory.GetFiles(database.MediaRoot).Length);
            using var preview = await client.GetAsync(kept.GetProperty("previewUrl").GetString());
            Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
            using var decoded = Image.NewFromBuffer(await preview.Content.ReadAsByteArrayAsync());
            Assert.Equal(8, decoded.Width);
        }
        await using var restarted = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var later = await restarted.CreateAuthenticatedClientAsync(subject);
        using var repeated = await later.DeleteAsync($"/api/photographs/{id}?revision=1");
        repeated.EnsureSuccessStatusCode();
        Assert.Equal(completed.GetRawText(), (await repeated.Content.ReadFromJsonAsync<JsonElement>()).GetRawText());
    }

    [Fact]
    public async Task L2_031_6_032_1_Storage_failure_stays_pending_and_resumes_after_worker_restart()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var before = Directory.Exists(database.MediaRoot) ? Directory.GetFiles(database.MediaRoot) : [];
        var photo = await PhotographFixture.UploadAsync(client);
        var files = Directory.GetFiles(database.MediaRoot).Except(before).ToArray();
        var blocked = files[0];
        var held = blocked + ".fixture";
        Assert.StartsWith(Path.GetFullPath(database.MediaRoot) + Path.DirectorySeparatorChar, Path.GetFullPath(blocked));
        File.Move(blocked, held);
        Directory.CreateDirectory(blocked);
        try
        {
            var id = photo.GetProperty("id").GetGuid();
            using var deleted = await client.DeleteAsync($"/api/photographs/{id}?revision=1");
            deleted.EnsureSuccessStatusCode();
            var operationId = (await deleted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            await using (var worker = new CleanupProcess(database.ConnectionString, database.MediaRoot))
            {
                // The other file disappearing proves this worker reached the manifest.
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                while (File.Exists(files[1]))
                {
                    await worker.EnsureRunningAsync();
                    await Task.Delay(50, deadline.Token);
                }
                var pending = await client.GetFromJsonAsync<JsonElement>($"/api/deletions/{operationId}");
                Assert.Equal("Pending", pending.GetProperty("status").GetString());
                using var hidden = await client.GetAsync($"/api/photographs/{id}/image");
                Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
            }
            Directory.Delete(blocked);
            File.Move(held, blocked);
            await using var restarted = new CleanupProcess(database.ConnectionString, database.MediaRoot);
            await WaitForCompletionAsync(client, operationId, restarted);
            Assert.All(files, file => Assert.False(File.Exists(file)));
        }
        finally
        {
            if (Directory.Exists(blocked)) Directory.Delete(blocked);
            if (File.Exists(held)) File.Move(held, blocked, overwrite: true);
        }
    }

    private static async Task<JsonElement> WaitForCompletionAsync(HttpClient client, Guid id, CleanupProcess worker)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (true)
        {
            await worker.EnsureRunningAsync();
            var status = await client.GetFromJsonAsync<JsonElement>($"/api/deletions/{id}", deadline.Token);
            if (status.GetProperty("status").GetString() == "Completed") return status;
            await Task.Delay(50, deadline.Token);
        }
    }
}
