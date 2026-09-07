// Given multiple multipart files, when uploading, then none is silently selected or saved.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NetVips;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class UploadFileCountTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData("image")]
    [InlineData("attachment")]
    public async Task L2_039_1_An_upload_rejects_extra_files_in_any_multipart_field(string extraField)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var image = Image.Black(8, 6, bands: 3);
        var bytes = image.PngsaveBuffer();
        var key = Guid.NewGuid().ToString();
        var filesBefore = Directory.Exists(database.MediaRoot) ? Directory.GetFiles(database.MediaRoot).Length : 0;
        using var multiple = new MultipartFormDataContent();
        multiple.Add(Part(bytes), "image", "First.png");
        multiple.Add(Part(bytes), extraField, "Second.png");
        using var rejected = await PhotographFixture.SubmitAsync(client, multiple, key);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var problem = await rejected.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("image", out _));
        var before = await client.GetFromJsonAsync<JsonElement>("/api/photographs");
        Assert.Empty(before.GetProperty("items").EnumerateArray());
        Assert.Equal(filesBefore, Directory.Exists(database.MediaRoot) ? Directory.GetFiles(database.MediaRoot).Length : 0);
        using var single = new MultipartFormDataContent();
        single.Add(Part(bytes), "image", "First.png");
        using var retry = await PhotographFixture.SubmitAsync(client, single, key);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        var after = await client.GetFromJsonAsync<JsonElement>("/api/photographs");
        Assert.Single(after.GetProperty("items").EnumerateArray());
    }

    private static ByteArrayContent Part(byte[] bytes)
    {
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return part;
    }
}
