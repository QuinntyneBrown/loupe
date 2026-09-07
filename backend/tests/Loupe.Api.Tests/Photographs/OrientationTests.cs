// L2-001.1: every EXIF orientation produces the same correctly positioned, complete frame in both browser image variants.
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using NetVips;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class OrientationTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData(1, 16, 12, 0)]
    [InlineData(2, 16, 12, 80)]
    [InlineData(3, 16, 12, 240)]
    [InlineData(4, 16, 12, 160)]
    [InlineData(5, 12, 16, 0)]
    [InlineData(6, 12, 16, 160)]
    [InlineData(7, 12, 16, 240)]
    [InlineData(8, 12, 16, 80)]
    public async Task L2_001_1_Orientation_is_applied_to_full_image_and_preview(int orientation, int width, int height, int topLeft)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync();
        using var black = Image.Black(8, 6, bands: 3);
        using var gray = black.NewFromImage(80);
        using var light = black.NewFromImage(160);
        using var white = black.NewFromImage(240);
        using var top = black.Join(gray, Enums.Direction.Horizontal);
        using var bottom = light.Join(white, Enums.Direction.Horizontal);
        using var quadrants = top.Join(bottom, Enums.Direction.Vertical);
        using var oriented = quadrants.Mutate(image => image.Set(GValue.GIntType, "orientation", orientation));
        using var upload = new MultipartFormDataContent();
        var part = new ByteArrayContent(oriented.JpegsaveBuffer(q: 100));
        part.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        upload.Add(part, "image", "Orientation.jpg");
        using var response = await PhotographFixture.SubmitAsync(client, upload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var saved = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        foreach (var variant in new[] { "imageUrl", "previewUrl" })
        {
            using var media = await client.GetAsync(saved.RootElement.GetProperty(variant).GetString());
            using var decoded = Image.NewFromBuffer(await media.Content.ReadAsByteArrayAsync());
            Assert.Equal(width, decoded.Width);
            Assert.Equal(height, decoded.Height);
            Assert.InRange(decoded.Getpoint(2, 2)[0], topLeft - 5, topLeft + 5);
        }
    }
}
