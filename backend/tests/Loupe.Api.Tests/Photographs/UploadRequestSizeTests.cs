// Given an oversized multipart body with or without Content-Length, when received,
// then it receives 413 without a photograph or managed media; a boundary-valid stream succeeds.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NetVips;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class UploadRequestSizeTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData(26_000_001, false, 413)]
    [InlineData(26_000_001, true, 413)]
    [InlineData(25_000_000, true, 201)]
    public async Task L2_001_2_039_1_Multipart_request_limits_have_the_defined_error(int size, bool streaming, int status)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var image = Image.Black(8, 6, bands: 3);
        var bytes = image.PngsaveBuffer();
        Array.Resize(ref bytes, size);
        var filesBefore = Directory.Exists(database.MediaRoot) ? Directory.GetFiles(database.MediaRoot).Length : 0;
        using var multipart = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipart.Add(part, "image", "Study.png");
        using HttpContent content = streaming ? new StreamingUploadContent(multipart) : multipart;
        using var response = await PhotographFixture.SubmitAsync(client, content);
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        if (status == 413)
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("image_too_large", problem.GetProperty("code").GetString());
            var list = await client.GetFromJsonAsync<JsonElement>("/api/photographs");
            Assert.Empty(list.GetProperty("items").EnumerateArray());
            Assert.Equal(filesBefore, Directory.Exists(database.MediaRoot) ? Directory.GetFiles(database.MediaRoot).Length : 0);
        }
    }
}
