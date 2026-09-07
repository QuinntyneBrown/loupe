// Given an animated or multi-image container, when uploaded, then it is rejected rather than silently saving the first frame.
// L2-001.2 and L2-039.1.
using System.Net;
using System.Net.Http.Headers;
using Loupe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetVips;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class MultiImageTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData("webp")]
    [InlineData("heic")]
    public async Task L2_039_1_Multiple_images_are_rejected_without_retaining_the_first_frame(string format)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync();
        using var black = Image.Black(32, 32, bands: 3);
        using var white = black.NewFromImage(255);
        using var frames = black.Join(white, Enums.Direction.Vertical);
        var bytes = format == "webp" ? frames.WebpsaveBuffer(pageHeight: 32) : frames.HeifsaveBuffer(pageHeight: 32, compression: Enums.ForeignHeifCompression.Hevc);
        using var decoded = Image.NewFromBuffer(bytes);
        Assert.Equal(2, decoded.Get("n-pages"));
        using var upload = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("image/" + format);
        upload.Add(part, "image", "Sequence." + format);
        using var response = await client.PostAsync("/api/photographs", upload);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<LibraryDbContext>().Photographs.CountAsync());
        Assert.Empty(Directory.Exists(database.MediaRoot) ? Directory.GetFiles(database.MediaRoot) : []);
    }
}
