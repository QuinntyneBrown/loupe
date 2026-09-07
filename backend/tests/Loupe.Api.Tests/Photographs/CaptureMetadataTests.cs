// Given camera settings mixed with private EXIF, when a photograph is saved, then only permitted settings survive and image copies are scrubbed.
// L2-003 and L2-041.1.
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using NetVips;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class CaptureMetadataTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_041_1_Retain_camera_settings_without_GPS_serial_or_owner_metadata()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync();
        using var blank = Image.Black(20, 10, bands: 3);
        using var tagged = blank.Mutate(image =>
        {
            image.Set(GValue.GStrType, "exif-ifd0-Model", "Fixture Camera");
            image.Set(GValue.GStrType, "exif-ifd2-ISOSpeedRatings", "200");
            image.Set(GValue.GStrType, "exif-ifd2-LensModel", "Fixture Lens");
            image.Set(GValue.GStrType, "exif-ifd2-FNumber", "28/10");
            image.Set(GValue.GStrType, "exif-ifd2-ExposureTime", "1/125");
            image.Set(GValue.GStrType, "exif-ifd2-FocalLength", "50/1");
            image.Set(GValue.GStrType, "exif-ifd2-DateTimeOriginal", "2026:06:15 09:30:00");
            image.Set(GValue.GStrType, "exif-ifd2-BodySerialNumber", "CONFIDENTIAL-SERIAL");
            image.Set(GValue.GStrType, "exif-ifd2-CameraOwnerName", "CONFIDENTIAL-OWNER");
            image.Set(GValue.GStrType, "exif-ifd3-GPSLatitudeRef", "N");
            image.Set(GValue.GStrType, "exif-ifd3-GPSLatitude", "43/1 39/1 0/1");
            image.Set(GValue.GIntType, "orientation", 6);
        });
        var bytes = tagged.JpegsaveBuffer();
        using var fixture = Image.NewFromBuffer(bytes);
        Assert.Contains("CONFIDENTIAL-OWNER", (string)fixture.Get("exif-ifd2-CameraOwnerName"));
        Assert.Contains("exif-ifd3-GPSLatitude", fixture.GetFields());
        using var upload = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        upload.Add(part, "image", "Study.jpg");
        using var response = await client.PostAsync("/api/photographs", upload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("CONFIDENTIAL", body);
        using var saved = JsonDocument.Parse(body);
        Assert.Equal("Fixture Camera", saved.RootElement.GetProperty("exif").GetProperty("camera").GetString());
        Assert.Equal("200", saved.RootElement.GetProperty("exif").GetProperty("iso").GetString());
        Assert.Equal("Fixture Lens", saved.RootElement.GetProperty("exif").GetProperty("lens").GetString());
        Assert.Equal("28/10", saved.RootElement.GetProperty("exif").GetProperty("aperture").GetString());
        Assert.Equal("1/125", saved.RootElement.GetProperty("exif").GetProperty("shutterSpeed").GetString());
        Assert.Equal("50/1", saved.RootElement.GetProperty("exif").GetProperty("focalLength").GetString());
        Assert.Equal("2026:06:15 09:30:00", saved.RootElement.GetProperty("exif").GetProperty("capturedAt").GetString());
        foreach (var key in new[] { "imageUrl", "previewUrl" })
        {
            using var media = await client.GetAsync(saved.RootElement.GetProperty(key).GetString());
            using var decoded = Image.NewFromBuffer(await media.Content.ReadAsByteArrayAsync());
            Assert.Equal(10, decoded.Width);
            Assert.Equal(20, decoded.Height);
            Assert.DoesNotContain(decoded.GetFields(), field => field.StartsWith("exif-", StringComparison.Ordinal));
        }
        await using var second = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var later = await second.CreateAuthenticatedClientAsync();
        using var revisited = await later.GetAsync(response.Headers.Location);
        using var retained = JsonDocument.Parse(await revisited.Content.ReadAsStringAsync());
        Assert.Equal("Fixture Camera", retained.RootElement.GetProperty("exif").GetProperty("camera").GetString());
    }
}
