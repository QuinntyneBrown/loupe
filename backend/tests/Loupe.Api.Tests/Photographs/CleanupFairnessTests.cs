// Given a full batch of blocked cleanup manifests, when a later healthy deletion
// awaits the same worker, then repeated failures cannot starve its cleanup.
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Persistence;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class CleanupFairnessTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_031_6_032_1_4_Failed_batch_yields_to_later_healthy_cleanup()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var blockedPaths = new List<string>();
        Guid firstOperation = default;
        try
        {
            for (var index = 0; index < 100; index++)
            {
                var before = Directory.Exists(database.MediaRoot) ? Directory.GetFiles(database.MediaRoot) : [];
                var photo = await PhotographFixture.UploadAsync(client);
                var blocked = Directory.GetFiles(database.MediaRoot).Except(before).First();
                Assert.StartsWith(Path.GetFullPath(database.MediaRoot) + Path.DirectorySeparatorChar, Path.GetFullPath(blocked));
                File.Move(blocked, blocked + ".fixture");
                blockedPaths.Add(blocked);
                Directory.CreateDirectory(blocked);
                using var deleted = await client.DeleteAsync($"/api/photographs/{photo.GetProperty("id").GetGuid()}?revision=1");
                deleted.EnsureSuccessStatusCode();
                var operation = await deleted.Content.ReadFromJsonAsync<JsonElement>();
                if (index == 0) firstOperation = operation.GetProperty("id").GetGuid();
                factory.Clock.Advance(TimeSpan.FromSeconds(6));
            }
            var healthy = await PhotographFixture.UploadAsync(client);
            using var accepted = await client.DeleteAsync($"/api/photographs/{healthy.GetProperty("id").GetGuid()}?revision=1");
            accepted.EnsureSuccessStatusCode();
            var healthyOperation = (await accepted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            await using var worker = new CleanupProcess(database.ConnectionString, database.MediaRoot);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (true)
            {
                await worker.EnsureRunningAsync();
                var status = await client.GetFromJsonAsync<JsonElement>($"/api/deletions/{healthyOperation}", deadline.Token);
                if (status.GetProperty("status").GetString() == "Completed") break;
                await Task.Delay(100, deadline.Token);
            }
            var pending = await client.GetFromJsonAsync<JsonElement>($"/api/deletions/{firstOperation}");
            Assert.Equal("Pending", pending.GetProperty("status").GetString());
        }
        finally
        {
            foreach (var blocked in blockedPaths)
            {
                if (Directory.Exists(blocked)) Directory.Delete(blocked);
                if (File.Exists(blocked + ".fixture")) File.Move(blocked + ".fixture", blocked, overwrite: true);
            }
        }
    }
}
