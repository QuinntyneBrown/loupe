// Given old abandoned upload bytes and saved media, when maintenance runs,
// then only old unreferenced managed files disappear from private storage.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Persistence;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class AbandonedMediaTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_032_2_Old_orphans_and_partial_files_are_removed_while_recent_and_referenced_media_survive()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var photo = await PhotographFixture.UploadAsync(client);
        var savedPaths = Directory.GetFiles(database.MediaRoot);
        foreach (var path in savedPaths) File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-2));
        var abandoned = await WriteAsync(Guid.NewGuid().ToString("N"), old: true);
        var partial = await WriteAsync(Guid.NewGuid().ToString("N") + ".partial", old: true);
        var recent = await WriteAsync(Guid.NewGuid().ToString("N"), old: false);
        var recentPartial = await WriteAsync(Guid.NewGuid().ToString("N") + ".partial", old: false);
        var unknown = await WriteAsync("keep-unmanaged.fixture", old: true);
        await using var worker = new CleanupProcess(database.ConnectionString, database.MediaRoot);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (File.Exists(abandoned) || File.Exists(partial))
        {
            await worker.EnsureRunningAsync();
            await Task.Delay(50, deadline.Token);
        }
        Assert.All(savedPaths, path => Assert.True(File.Exists(path)));
        Assert.All(new[] { recent, recentPartial, unknown }, path => Assert.True(File.Exists(path)));
        using var image = await client.GetAsync(photo.GetProperty("imageUrl").GetString());
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        using var preview = await client.GetAsync(photo.GetProperty("previewUrl").GetString());
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
    }

    [Fact]
    public async Task L2_001_4_032_2_Cleanup_cannot_remove_media_from_an_upload_waiting_to_commit()
    {
        var pause = new PausedCommit();
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot) { TransactionInterceptor = pause };
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var sentinel = await PhotographFixture.UploadAsync(client);
        using var deleted = await client.DeleteAsync($"/api/photographs/{sentinel.GetProperty("id").GetGuid()}?revision=1");
        deleted.EnsureSuccessStatusCode();
        var deletionId = (await deleted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var before = Directory.GetFiles(database.MediaRoot);
        pause.Arm();
        var upload = PhotographFixture.UploadAsync(client, "Keep the in-flight upload");
        JsonElement saved;
        try
        {
            await pause.Reached.WaitAsync(TimeSpan.FromSeconds(15));
            var uploadingFiles = Directory.GetFiles(database.MediaRoot).Except(before).ToArray();
            Assert.Equal(2, uploadingFiles.Length);
            foreach (var path in uploadingFiles) File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-2));
            await using var worker = new CleanupProcess(database.ConnectionString, database.MediaRoot);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (true)
            {
                await worker.EnsureRunningAsync();
                var status = await client.GetFromJsonAsync<JsonElement>($"/api/deletions/{deletionId}", deadline.Token);
                if (status.GetProperty("status").GetString() == "Completed") break;
                await Task.Delay(50, deadline.Token);
            }
            await Task.Delay(TimeSpan.FromSeconds(2));
            Assert.All(uploadingFiles, path => Assert.True(File.Exists(path)));
            pause.Resume();
            saved = await upload;
        }
        finally
        {
            pause.Resume();
            await upload;
        }
        using var image = await client.GetAsync(saved.GetProperty("imageUrl").GetString());
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        using var preview = await client.GetAsync(saved.GetProperty("previewUrl").GetString());
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
    }

    private async Task<string> WriteAsync(string name, bool old)
    {
        var path = Path.Combine(database.MediaRoot, name);
        await File.WriteAllTextAsync(path, "Synthetic abandoned upload bytes");
        if (old) File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-2));
        return path;
    }
}
