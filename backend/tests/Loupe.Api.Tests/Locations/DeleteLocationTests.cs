using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Persistence;
using Loupe.Api.Tests.Photographs;
using Loupe.Api.Tests.References;
using Xunit;

namespace Loupe.Api.Tests.Locations;

// Acceptance Test
// Traces to: L2-057, L2-031, L2-032
// Description: Delete a location so it disappears immediately from the grid, detail,
// media and counts, its bytes are cleaned up by the worker while everything else the
// owner keeps stays readable, and repeated or foreign deletions follow the shared rules.
public sealed class DeleteLocationTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    // L2-057, criterion 6; L2-031, criterion 7; L2-032, criterion 2: deletion revokes the location, cleans its media, and spares the rest.
    [Fact]
    public async Task L2_057_6_L2_031_7_L2_032_2_Deleting_revokes_the_location_cleans_its_media_and_keeps_everything_else()
    {
        var subject = Guid.NewGuid().ToString();
        Guid id, kept;
        string? keptImageUrl;
        JsonElement operation;
        await using (var factory = new ApiFactory(database.ConnectionString, database.MediaRoot))
        {
            using var owner = await factory.CreateAuthenticatedClientAsync(subject);
            id = (await LocationFixture.CreateAsync(owner, new { name = "Doomed", notes = "Notes", tags = new[] { new { name = "Riverside", category = "subject" } } })).GetProperty("id").GetGuid();
            var beforeDoomed = Directory.Exists(database.MediaRoot) ? Directory.GetFiles(database.MediaRoot) : [];
            JsonElement doomed = default;
            for (var index = 0; index < 2; index++) doomed = await LocationFixture.AddImageAsync(owner, id);
            var doomedFiles = Directory.GetFiles(database.MediaRoot).Except(beforeDoomed).ToArray();
            Assert.Equal(4, doomedFiles.Length);
            kept = (await LocationFixture.CreateAsync(owner, new { name = "Kept" })).GetProperty("id").GetGuid();
            var keptLocation = await LocationFixture.AddImageAsync(owner, kept);
            keptImageUrl = keptLocation.GetProperty("images")[0].GetProperty("imageUrl").GetString();
            using var reference = await ReferenceFixture.SubmitAsync(owner);
            reference.EnsureSuccessStatusCode();
            await PhotographFixture.UploadAsync(owner);
            var imageUrls = doomed.GetProperty("images").EnumerateArray().Select(image => image.GetProperty("imageUrl").GetString()!).ToArray();
            var previewUrls = doomed.GetProperty("images").EnumerateArray().Select(image => image.GetProperty("previewUrl").GetString()!).ToArray();

            using var deleted = await owner.DeleteAsync($"/api/locations/{id}?revision={doomed.GetProperty("revision").GetInt64()}");
            Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
            operation = await deleted.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(id, operation.GetProperty("resourceId").GetGuid());
            Assert.Equal("Pending", operation.GetProperty("status").GetString());
            foreach (var url in new[] { $"/api/locations/{id}" }.Concat(imageUrls).Concat(previewUrls))
            {
                using var missing = await owner.GetAsync(url);
                Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            }
            var page = await owner.GetFromJsonAsync<JsonElement>("/api/locations");
            Assert.Equal(kept, Assert.Single(page.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
            Assert.Equal(1, page.GetProperty("totalCount").GetInt32());

            await using var worker = new CleanupProcess(database.ConnectionString, database.MediaRoot);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            JsonElement completed;
            while ((completed = await owner.GetFromJsonAsync<JsonElement>($"/api/deletions/{operation.GetProperty("id").GetGuid()}")).GetProperty("status").GetString() != "Completed")
            {
                await worker.EnsureRunningAsync();
                await Task.Delay(50, deadline.Token);
            }
            Assert.All(doomedFiles, path => Assert.False(File.Exists(path)));
            using var keptImage = await owner.GetAsync(keptImageUrl);
            Assert.Equal(HttpStatusCode.OK, keptImage.StatusCode);
            Assert.Single((await owner.GetFromJsonAsync<JsonElement>("/api/references")).GetProperty("items").EnumerateArray());
            Assert.Single((await owner.GetFromJsonAsync<JsonElement>("/api/photographs")).GetProperty("items").EnumerateArray());
        }
        await using var restart = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var later = await restart.CreateAuthenticatedClientAsync(subject);
        using var replay = await later.DeleteAsync($"/api/locations/{id}?revision=3");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(operation.GetProperty("id").GetGuid(), (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        Assert.Equal("Kept", (await later.GetFromJsonAsync<JsonElement>($"/api/locations/{kept}")).GetProperty("name").GetString());
    }

    // L2-057, criterion 7; L2-031, criterion 5: foreign, stale, and invalid deletions change nothing.
    [Fact]
    public async Task L2_057_7_L2_031_5_Foreign_stale_and_invalid_deletions_preserve_the_location()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await LocationFixture.CreateAsync(owner)).GetProperty("id").GetGuid();
        var before = await owner.GetStringAsync($"/api/locations/{id}");
        using var foreign = await stranger.DeleteAsync($"/api/locations/{id}?revision=1");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var unknown = await owner.DeleteAsync($"/api/locations/{Guid.NewGuid()}?revision=1");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        using var stale = await owner.DeleteAsync($"/api/locations/{id}?revision=2");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var invalid = await owner.DeleteAsync($"/api/locations/{id}?revision=0");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(before, await owner.GetStringAsync($"/api/locations/{id}"));
    }

    // L2-056, criterion 7: a deleted location rejects image operations with 404 and writes nothing.
    [Fact]
    public async Task L2_056_7_A_deleted_location_rejects_image_operations_without_writing_files()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await LocationFixture.CreateAsync(owner)).GetProperty("id").GetGuid();
        var location = await LocationFixture.AddImageAsync(owner, id);
        var imageId = LocationFixture.ImageIds(location)[0];
        using var deleted = await owner.DeleteAsync($"/api/locations/{id}?revision={location.GetProperty("revision").GetInt64()}");
        deleted.EnsureSuccessStatusCode();
        var before = Directory.GetFiles(database.MediaRoot).Length;
        using var upload = await LocationFixture.SubmitImageAsync(owner, id);
        Assert.Equal(HttpStatusCode.NotFound, upload.StatusCode);
        using var cover = await owner.PutAsJsonAsync($"/api/locations/{id}/cover", new { revision = 2, imageId });
        Assert.Equal(HttpStatusCode.NotFound, cover.StatusCode);
        using var remove = await owner.DeleteAsync($"/api/locations/{id}/images/{imageId}?revision=2");
        Assert.Equal(HttpStatusCode.NotFound, remove.StatusCode);
        Assert.Equal(before, Directory.GetFiles(database.MediaRoot).Length);
    }
}
