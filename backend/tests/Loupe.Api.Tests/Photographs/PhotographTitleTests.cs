// L2-001.3 and shared field limits: normalize scalar-counted titles, safely derive defaults and reject invalid values before retaining media.
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Loupe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetVips;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class PhotographTitleTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData("normalized")]
    [InlineData("scalar-boundary")]
    [InlineData("too-long")]
    [InlineData("filename-default")]
    [InlineData("empty-filename")]
    [InlineData("traversal-filename")]
    public async Task L2_001_3_Title_normalization_and_defaults_are_consistent(string scenario)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync();
        var emoji = string.Concat(Enumerable.Repeat("📷", scenario == "too-long" ? 201 : 200));
        var title = scenario switch { "normalized" => "  Window\r\nlight\rStudy  ", "scalar-boundary" or "too-long" => emoji, _ => null };
        var filename = scenario switch { "filename-default" => emoji + "extra.png", "empty-filename" => ".png", "traversal-filename" => "../../escaped.png", _ => "Study.png" };
        using var image = Image.Black(8, 6, bands: 3);
        using var upload = new MultipartFormDataContent();
        var part = new ByteArrayContent(image.PngsaveBuffer());
        part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        upload.Add(part, "image", filename);
        if (title is not null) upload.Add(new StringContent(title), "title");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var before = await db.Photographs.CountAsync();
        var filesBefore = Directory.Exists(database.MediaRoot) ? Directory.GetFiles(database.MediaRoot).Length : 0;
        using var response = await PhotographFixture.SubmitAsync(client, upload);
        if (scenario == "too-long")
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(before, await db.Photographs.CountAsync());
            Assert.Equal(filesBefore, Directory.Exists(database.MediaRoot) ? Directory.GetFiles(database.MediaRoot).Length : 0);
            return;
        }
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var saved = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var expected = scenario switch { "normalized" => "Window\nlight\nStudy", "scalar-boundary" or "filename-default" => emoji, "empty-filename" => "Untitled photograph", _ => "escaped" };
        Assert.Equal(expected, saved.RootElement.GetProperty("title").GetString());
    }
}
