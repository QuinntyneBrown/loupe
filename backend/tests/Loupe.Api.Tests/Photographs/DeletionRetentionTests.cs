// Given deletion records of different ages, when maintenance runs, then the
// 35-day journal is retained until expiry and unfinished cleanup is never lost.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Persistence;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class DeletionRetentionTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData(34, false, true)]
    [InlineData(36, false, false)]
    [InlineData(36, true, true)]
    public async Task L2_031_5_032_1_Expired_completed_records_are_pruned_but_recent_and_pending_records_survive(int days, bool blocked, bool retained)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        factory.Clock.Advance(-TimeSpan.FromDays(days));
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var before = Directory.Exists(database.MediaRoot) ? Directory.GetFiles(database.MediaRoot) : [];
        var photo = await PhotographFixture.UploadAsync(client);
        var files = Directory.GetFiles(database.MediaRoot).Except(before).ToArray();
        var obstacle = files[0];
        if (blocked)
        {
            Assert.StartsWith(Path.GetFullPath(database.MediaRoot) + Path.DirectorySeparatorChar, Path.GetFullPath(obstacle));
            File.Move(obstacle, obstacle + ".fixture");
            Directory.CreateDirectory(obstacle);
        }
        try
        {
            var id = photo.GetProperty("id").GetGuid();
            using var deleted = await client.DeleteAsync($"/api/photographs/{id}?revision=1");
            deleted.EnsureSuccessStatusCode();
            var operationId = (await deleted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            await using var worker = new CleanupProcess(database.ConnectionString, database.MediaRoot);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (true)
            {
                await worker.EnsureRunningAsync();
                using var read = await client.GetAsync($"/api/deletions/{operationId}", deadline.Token);
                if (!retained && read.StatusCode == HttpStatusCode.NotFound) break;
                if (retained && read.StatusCode == HttpStatusCode.OK)
                {
                    var status = await read.Content.ReadFromJsonAsync<JsonElement>(deadline.Token);
                    if (!blocked && status.GetProperty("status").GetString() == "Completed") break;
                    if (blocked && !File.Exists(files[1]))
                    {
                        Assert.Equal("Pending", status.GetProperty("status").GetString());
                        break;
                    }
                }
                await Task.Delay(50, deadline.Token);
            }
            using var repeated = await client.DeleteAsync($"/api/photographs/{id}?revision=1");
            Assert.Equal(retained ? HttpStatusCode.OK : HttpStatusCode.NotFound, repeated.StatusCode);
            using var unavailable = await client.GetAsync($"/api/photographs/{id}/image");
            Assert.Equal(HttpStatusCode.NotFound, unavailable.StatusCode);
        }
        finally
        {
            if (blocked)
            {
                if (Directory.Exists(obstacle)) Directory.Delete(obstacle);
                if (File.Exists(obstacle + ".fixture")) File.Move(obstacle + ".fixture", obstacle, overwrite: true);
            }
        }
    }
}
