// Given an invalid upload, when posted directly, then its documented error is returned without a record or retained media.
// L2-001.2, L2-039.1.
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Loupe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetVips;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class UploadValidationTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData("svg", "image/svg+xml", 415)]
    [InlineData("executable", "image/png", 415)]
    [InlineData("png", "image/jpeg", 415)]
    [InlineData("corrupt", "image/png", 422)]
    [InlineData("wide", "image/png", 422)]
    [InlineData("tall", "image/png", 422)]
    [InlineData("too-large", "image/png", 413)]
    [InlineData("pixels", "image/png", 422)]
    public async Task L2_001_2_039_1_Invalid_upload_has_no_saved_side_effects(string fixture, string declaredType, int expectedStatus)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync();
        var bytes = CreateFixture(fixture);
        using var upload = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(declaredType);
        upload.Add(part, "image", "innocent.png");
        using var response = await PhotographFixture.SubmitAsync(client, upload);
        Assert.Equal((HttpStatusCode)expectedStatus, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<LibraryDbContext>().Photographs.CountAsync());
        Assert.Empty(Directory.Exists(database.MediaRoot) ? Directory.GetFiles(database.MediaRoot) : []);
    }

    private static byte[] CreateFixture(string fixture)
    {
        if (fixture == "svg") return Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg' width='8' height='6'><rect width='8' height='6'/></svg>");
        if (fixture == "executable") return Encoding.ASCII.GetBytes("MZ-not-an-image");
        if (fixture == "corrupt") return [137, 80, 78, 71, 13, 10, 26, 10, 0, 0];
        using var image = Image.Black(fixture == "pixels" ? 10001 : fixture == "wide" ? 20001 : 8,
            fixture == "pixels" ? 10000 : fixture == "tall" ? 20001 : 6, bands: 3);
        var bytes = image.PngsaveBuffer();
        if (fixture == "too-large") Array.Resize(ref bytes, 25_000_001);
        return bytes;
    }
}
