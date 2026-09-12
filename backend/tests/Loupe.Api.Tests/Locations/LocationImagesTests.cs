using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Persistence;
using NetVips;
using Xunit;

namespace Loupe.Api.Tests.Locations;

// Acceptance Test
// Traces to: L2-056
// Description: Add up to ten images to a location one operation at a time, read them
// back in order with oriented previews and a cover, remove or re-cover them under
// revision protection, and keep every retained copy free of private EXIF.
public sealed class LocationImagesTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    // L2-056, criterion 1: each supported format becomes an ordered image with an oriented preview; the first is the cover.
    [Fact]
    public async Task L2_056_1_Each_supported_format_becomes_an_ordered_image_with_an_oriented_preview_and_the_first_is_the_cover()
    {
        var subject = Guid.NewGuid().ToString();
        Guid id;
        var uploaded = new List<Guid>();
        await using (var factory = new ApiFactory(database.ConnectionString, database.MediaRoot))
        {
            using var owner = await factory.CreateAuthenticatedClientAsync(subject);
            id = (await LocationFixture.CreateAsync(owner)).GetProperty("id").GetGuid();
            foreach (var format in new[] { "png", "jpeg", "webp", "heic" })
            {
                var location = await LocationFixture.AddImageAsync(owner, id, format);
                uploaded.Add(LocationFixture.ImageIds(location)[^1]);
            }
            using var portrait = Image.Black(20, 10, bands: 3);
            using var rotated = portrait.Mutate(image => image.Set(GValue.GIntType, "orientation", 6));
            using var oriented = await LocationFixture.SubmitImageAsync(owner, id, rotated.JpegsaveBuffer(), "image/jpeg");
            Assert.Equal(HttpStatusCode.Created, oriented.StatusCode);
            uploaded.Add(LocationFixture.ImageIds(await oriented.Content.ReadFromJsonAsync<JsonElement>())[^1]);
        }
        await using var restart = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var later = await restart.CreateAuthenticatedClientAsync(subject);
        var saved = await later.GetFromJsonAsync<JsonElement>($"/api/locations/{id}");
        Assert.Equal(uploaded, LocationFixture.ImageIds(saved));
        Assert.Equal(uploaded[0], saved.GetProperty("coverImageId").GetGuid());
        var images = saved.GetProperty("images").EnumerateArray().ToArray();
        for (var index = 0; index < images.Length; index++)
        {
            Assert.Equal(index + 1, images[index].GetProperty("position").GetInt32());
            var (width, height) = index == 4 ? (10, 20) : (8, 6);
            Assert.Equal(width, images[index].GetProperty("width").GetInt32());
            Assert.Equal(height, images[index].GetProperty("height").GetInt32());
            using var full = await later.GetAsync(images[index].GetProperty("imageUrl").GetString());
            Assert.Equal(HttpStatusCode.OK, full.StatusCode);
            Assert.Equal("image/png", full.Content.Headers.ContentType?.MediaType);
            using var decodedFull = Image.NewFromBuffer(await full.Content.ReadAsByteArrayAsync());
            Assert.Equal((width, height), (decodedFull.Width, decodedFull.Height));
            using var preview = await later.GetAsync(images[index].GetProperty("previewUrl").GetString());
            Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
            Assert.Equal("image/jpeg", preview.Content.Headers.ContentType?.MediaType);
            using var decodedPreview = Image.NewFromBuffer(await preview.Content.ReadAsByteArrayAsync());
            Assert.Equal((width, height), (decodedPreview.Width, decodedPreview.Height));
        }
        var card = Assert.Single((await later.GetFromJsonAsync<JsonElement>("/api/locations")).GetProperty("items").EnumerateArray());
        Assert.Equal(5, card.GetProperty("imageCount").GetInt32());
        Assert.Equal(images[0].GetProperty("previewUrl").GetString(), card.GetProperty("coverPreviewUrl").GetString());
    }

    // L2-056, criterion 2: an eleventh upload is a field error naming images, and no bytes are retained.
    [Fact]
    public async Task L2_056_2_An_eleventh_upload_is_rejected_with_no_bytes_retained()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var id = (await LocationFixture.CreateAsync(owner)).GetProperty("id").GetGuid();
        for (var index = 0; index < 10; index++) await LocationFixture.AddImageAsync(owner, id);
        var before = Directory.GetFiles(database.MediaRoot).Length;
        using var rejected = await LocationFixture.SubmitImageAsync(owner, id);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var error = await rejected.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_request", error.GetProperty("code").GetString());
        Assert.True(error.GetProperty("errors").TryGetProperty("images", out _), error.GetRawText());
        Assert.Equal(before, Directory.GetFiles(database.MediaRoot).Length);
        Assert.Equal(10, (await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}")).GetProperty("images").GetArrayLength());
    }

    // L2-056, criterion 3: each upload is its own operation; a replayed key adds nothing and an invalid file affects only itself.
    [Fact]
    public async Task L2_056_3_Each_upload_is_its_own_operation_and_an_invalid_file_affects_only_itself()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var id = (await LocationFixture.CreateAsync(owner)).GetProperty("id").GetGuid();
        using var first = await LocationFixture.SubmitImageAsync(owner, id, "first");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var afterFirst = await first.Content.ReadFromJsonAsync<JsonElement>();
        using var replay = await LocationFixture.SubmitImageAsync(owner, id, "first");
        replay.EnsureSuccessStatusCode();
        Assert.Equal(LocationFixture.ImageIds(afterFirst), LocationFixture.ImageIds(await replay.Content.ReadFromJsonAsync<JsonElement>()));
        var before = Directory.GetFiles(database.MediaRoot).Length;
        using var invalid = await LocationFixture.SubmitImageAsync(owner, id, "not an image"u8.ToArray(), "image/png", "second");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, invalid.StatusCode);
        Assert.Equal(before, Directory.GetFiles(database.MediaRoot).Length);
        using var third = await LocationFixture.SubmitImageAsync(owner, id, "third");
        Assert.Equal(HttpStatusCode.Created, third.StatusCode);
        var saved = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}");
        Assert.Equal(2, saved.GetProperty("images").GetArrayLength());
        Assert.Equal(LocationFixture.ImageIds(afterFirst)[0], LocationFixture.ImageIds(saved)[0]);
    }

    // L2-056, criterion 5: removing an image keeps the others in order, moves the cover when needed, and releases the bytes.
    [Fact]
    public async Task L2_056_5_Removing_an_image_keeps_order_moves_the_cover_and_releases_its_bytes()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var id = (await LocationFixture.CreateAsync(owner)).GetProperty("id").GetGuid();
        JsonElement location = default;
        for (var index = 0; index < 3; index++) location = await LocationFixture.AddImageAsync(owner, id);
        var (a, b, c) = (LocationFixture.ImageIds(location)[0], LocationFixture.ImageIds(location)[1], LocationFixture.ImageIds(location)[2]);
        var urls = location.GetProperty("images").EnumerateArray().ToDictionary(image => image.GetProperty("id").GetGuid(), image => (image.GetProperty("imageUrl").GetString(), image.GetProperty("previewUrl").GetString()));
        var filesBefore = Directory.GetFiles(database.MediaRoot).Length;

        using var removedMiddle = await owner.DeleteAsync($"/api/locations/{id}/images/{b}?revision={location.GetProperty("revision").GetInt64()}");
        Assert.Equal(HttpStatusCode.OK, removedMiddle.StatusCode);
        var afterMiddle = await removedMiddle.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal([a, c], LocationFixture.ImageIds(afterMiddle));
        Assert.Equal([1, 2], afterMiddle.GetProperty("images").EnumerateArray().Select(image => image.GetProperty("position").GetInt32()).ToArray());
        Assert.Equal(a, afterMiddle.GetProperty("coverImageId").GetGuid());
        using var gone = await owner.GetAsync(urls[b].Item1);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);

        using var removedCover = await owner.DeleteAsync($"/api/locations/{id}/images/{a}?revision={afterMiddle.GetProperty("revision").GetInt64()}");
        Assert.Equal(HttpStatusCode.OK, removedCover.StatusCode);
        var afterCover = await removedCover.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal([c], LocationFixture.ImageIds(afterCover));
        Assert.Equal(c, afterCover.GetProperty("coverImageId").GetGuid());
        var card = Assert.Single((await owner.GetFromJsonAsync<JsonElement>("/api/locations")).GetProperty("items").EnumerateArray());
        Assert.Equal(1, card.GetProperty("imageCount").GetInt32());
        Assert.Equal(urls[c].Item2, card.GetProperty("coverPreviewUrl").GetString());

        await using var worker = new CleanupProcess(database.ConnectionString, database.MediaRoot);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (Directory.GetFiles(database.MediaRoot).Length > filesBefore - 4)
        {
            await worker.EnsureRunningAsync();
            await Task.Delay(50, deadline.Token);
        }
        using var kept = await owner.GetAsync(urls[c].Item1);
        Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
    }

    // L2-056, criterion 6: a chosen cover persists after reload and the order is unchanged.
    [Fact]
    public async Task L2_056_6_A_chosen_cover_persists_across_restart_with_the_order_unchanged()
    {
        var subject = Guid.NewGuid().ToString();
        Guid id, chosen;
        Guid[] order;
        string? preview;
        await using (var factory = new ApiFactory(database.ConnectionString, database.MediaRoot))
        {
            using var owner = await factory.CreateAuthenticatedClientAsync(subject);
            id = (await LocationFixture.CreateAsync(owner)).GetProperty("id").GetGuid();
            JsonElement location = default;
            for (var index = 0; index < 3; index++) location = await LocationFixture.AddImageAsync(owner, id);
            order = LocationFixture.ImageIds(location);
            chosen = order[2];
            preview = location.GetProperty("images").EnumerateArray().Last().GetProperty("previewUrl").GetString();
            using var cover = await owner.PutAsJsonAsync($"/api/locations/{id}/cover", new { revision = location.GetProperty("revision").GetInt64(), imageId = chosen });
            Assert.Equal(HttpStatusCode.OK, cover.StatusCode);
            using var stale = await owner.PutAsJsonAsync($"/api/locations/{id}/cover", new { revision = location.GetProperty("revision").GetInt64(), imageId = order[1] });
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        }
        await using var restart = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var later = await restart.CreateAuthenticatedClientAsync(subject);
        var saved = await later.GetFromJsonAsync<JsonElement>($"/api/locations/{id}");
        Assert.Equal(chosen, saved.GetProperty("coverImageId").GetGuid());
        Assert.Equal(order, LocationFixture.ImageIds(saved));
        var card = Assert.Single((await later.GetFromJsonAsync<JsonElement>("/api/locations")).GetProperty("items").EnumerateArray());
        Assert.Equal(preview, card.GetProperty("coverPreviewUrl").GetString());
    }

    // L2-056, criterion 7: a foreign or unknown location rejects every image operation with 404 and writes nothing.
    [Fact]
    public async Task L2_056_7_Foreign_or_unknown_locations_reject_image_operations_without_writing_files()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync();
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await LocationFixture.CreateAsync(owner)).GetProperty("id").GetGuid();
        var location = await LocationFixture.AddImageAsync(owner, id);
        var imageId = LocationFixture.ImageIds(location)[0];
        var original = (await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}")).GetRawText();
        var before = Directory.GetFiles(database.MediaRoot).Length;
        foreach (var client in new[] { stranger })
        {
            using var upload = await LocationFixture.SubmitImageAsync(client, id);
            Assert.Equal(HttpStatusCode.NotFound, upload.StatusCode);
            using var cover = await client.PutAsJsonAsync($"/api/locations/{id}/cover", new { revision = 2, imageId });
            Assert.Equal(HttpStatusCode.NotFound, cover.StatusCode);
            using var remove = await client.DeleteAsync($"/api/locations/{id}/images/{imageId}?revision=2");
            Assert.Equal(HttpStatusCode.NotFound, remove.StatusCode);
            using var image = await client.GetAsync($"/api/locations/{id}/images/{imageId}");
            Assert.Equal(HttpStatusCode.NotFound, image.StatusCode);
            using var preview = await client.GetAsync($"/api/locations/{id}/images/{imageId}/preview");
            Assert.Equal(HttpStatusCode.NotFound, preview.StatusCode);
        }
        using var unknown = await LocationFixture.SubmitImageAsync(owner, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        using var unknownImage = await owner.PutAsJsonAsync($"/api/locations/{id}/cover", new { revision = 2, imageId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, unknownImage.StatusCode);
        Assert.Equal(before, Directory.GetFiles(database.MediaRoot).Length);
        Assert.Equal(original, (await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}")).GetRawText());
    }

    // L2-056, criterion 8: GPS, serial number, and owner name never survive, and the typed coordinates are untouched.
    [Fact]
    public async Task L2_056_8_Private_EXIF_is_stripped_from_every_copy_and_coordinates_stay_as_entered()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var typed = (await LocationFixture.CreateAsync(owner, new { name = "Typed", coordinates = new { latitude = "51.487213", longitude = "-0.287604" } })).GetProperty("id").GetGuid();
        var untyped = (await LocationFixture.CreateAsync(owner, new { name = "Untyped" })).GetProperty("id").GetGuid();
        using var blank = Image.Black(20, 10, bands: 3);
        using var tagged = blank.Mutate(image =>
        {
            image.Set(GValue.GStrType, "exif-ifd0-Model", "Fixture Camera");
            image.Set(GValue.GStrType, "exif-ifd2-BodySerialNumber", "CONFIDENTIAL-SERIAL");
            image.Set(GValue.GStrType, "exif-ifd2-CameraOwnerName", "CONFIDENTIAL-OWNER");
            image.Set(GValue.GStrType, "exif-ifd3-GPSLatitudeRef", "N");
            image.Set(GValue.GStrType, "exif-ifd3-GPSLatitude", "43/1 39/1 0/1");
            image.Set(GValue.GStrType, "exif-ifd3-GPSLongitudeRef", "W");
            image.Set(GValue.GStrType, "exif-ifd3-GPSLongitude", "79/1 23/1 0/1");
        });
        var bytes = tagged.JpegsaveBuffer();
        foreach (var id in new[] { typed, untyped })
        {
            using var response = await LocationFixture.SubmitImageAsync(owner, id, bytes, "image/jpeg");
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("CONFIDENTIAL", body);
            Assert.DoesNotContain("GPS", body);
            var saved = JsonDocument.Parse(body).RootElement;
            var image = Assert.Single(saved.GetProperty("images").EnumerateArray());
            foreach (var key in new[] { "imageUrl", "previewUrl" })
            {
                using var media = await owner.GetAsync(image.GetProperty(key).GetString());
                using var decoded = Image.NewFromBuffer(await media.Content.ReadAsByteArrayAsync());
                Assert.DoesNotContain(decoded.GetFields(), field => field.StartsWith("exif-", StringComparison.Ordinal));
            }
            if (id == typed)
            {
                Assert.Equal("51.487213", saved.GetProperty("coordinates").GetProperty("latitude").GetString());
                Assert.Equal("-0.287604", saved.GetProperty("coordinates").GetProperty("longitude").GetString());
            }
            else Assert.Equal(JsonValueKind.Null, saved.GetProperty("coordinates").ValueKind);
        }
    }
}
