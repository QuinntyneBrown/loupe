// Given supported still-image formats and inclusive boundaries, when uploaded, then full-frame browser images are readable.
// L2-001.1/.2 and L2-039.1.
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using NetVips;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class SupportedImageTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData("png", 8, 6, 0)]
    [InlineData("jpeg", 8, 6, 0)]
    [InlineData("webp", 8, 6, 0)]
    [InlineData("heic", 8, 6, 0)]
    [InlineData("png", 8, 6, 25_000_000)]
    [InlineData("png", 20_000, 6, 0)]
    [InlineData("png", 8, 20_000, 0)]
    [InlineData("png", 10_000, 10_000, 0)]
    public async Task L2_001_1_2_Supported_images_and_inclusive_limits_are_accepted(string format, int width, int height, int size)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync();
        using var image = Image.Black(width, height, bands: 3);
        var bytes = format switch
        {
            "jpeg" => image.JpegsaveBuffer(),
            "webp" => image.WebpsaveBuffer(),
            "heic" => image.HeifsaveBuffer(compression: Enums.ForeignHeifCompression.Hevc),
            _ => image.PngsaveBuffer()
        };
        if (size > 0) Array.Resize(ref bytes, size);
        using var upload = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("image/" + format);
        upload.Add(part, "image", "Study." + format);
        using var response = await PhotographFixture.SubmitAsync(client, upload);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        using var saved = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        using var full = await client.GetAsync(saved.RootElement.GetProperty("imageUrl").GetString());
        Assert.Equal(HttpStatusCode.OK, full.StatusCode);
        Assert.Equal("image/png", full.Content.Headers.ContentType?.MediaType);
        using var decoded = Image.NewFromBuffer(await full.Content.ReadAsByteArrayAsync());
        Assert.Equal(width, decoded.Width);
        Assert.Equal(height, decoded.Height);
    }
}
