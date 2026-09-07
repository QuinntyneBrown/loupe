// L2-001.3/L2-002: optional critique instructions are normalized and persisted independently of analysis.
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using NetVips;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class UploadBriefTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData("Beginner")]
    [InlineData("Intermediate")]
    [InlineData("Advanced")]
    [InlineData("Professional")]
    public async Task L2_002_2_Maximum_brief_fields_and_each_experience_are_accepted(string experience)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync();
        using var image = Image.Black(8, 6, bands: 3);
        using var upload = new MultipartFormDataContent();
        var part = new ByteArrayContent(image.PngsaveBuffer());
        part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        upload.Add(part, "image", "Study.png");
        var intent = string.Concat(Enumerable.Repeat("📷", 2000));
        var genre = new string('a', 100);
        var feedback = new string('b', 2000);
        upload.Add(new StringContent(intent), "intent");
        upload.Add(new StringContent(genre), "genre");
        upload.Add(new StringContent(feedback), "requestedFeedback");
        upload.Add(new StringContent(experience), "experience");
        using var response = await client.PostAsync("/api/photographs", upload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var saved = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var brief = saved.RootElement.GetProperty("brief");
        Assert.Equal(intent, brief.GetProperty("intent").GetString());
        Assert.Equal(genre, brief.GetProperty("genre").GetString());
        Assert.Equal(feedback, brief.GetProperty("requestedFeedback").GetString());
        Assert.Equal(experience, brief.GetProperty("experience").GetString());
    }

    [Theory]
    [InlineData("intent", 2001)]
    [InlineData("genre", 101)]
    [InlineData("requestedFeedback", 2001)]
    [InlineData("experience", 0)]
    public async Task L2_002_2_Invalid_brief_is_rejected_before_saving(string field, int length)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync();
        using var image = Image.Black(8, 6, bands: 3);
        using var upload = new MultipartFormDataContent();
        var part = new ByteArrayContent(image.PngsaveBuffer());
        part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        upload.Add(part, "image", "Study.png");
        upload.Add(new StringContent(length == 0 ? "Expert" : new string('a', length)), field);
        using var response = await client.PostAsync("/api/photographs", upload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task L2_002_1_4_Save_and_revisit_a_normalized_or_absent_brief(bool empty)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync();
        using var image = Image.Black(8, 6, bands: 3);
        using var upload = new MultipartFormDataContent();
        var part = new ByteArrayContent(image.PngsaveBuffer());
        part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        upload.Add(part, "image", "Study.png");
        upload.Add(new StringContent(empty ? "  " : "  Show quiet\r\nmorning light  "), "intent");
        upload.Add(new StringContent(empty ? "" : "  Portrait  "), "genre");
        upload.Add(new StringContent(empty ? "" : "Beginner"), "experience");
        upload.Add(new StringContent(empty ? "\r\n" : "  Improve separation\rand framing  "), "requestedFeedback");
        using var response = await client.PostAsync("/api/photographs", upload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var second = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var later = await second.CreateAuthenticatedClientAsync();
        using var revisited = await later.GetAsync(response.Headers.Location);
        using var saved = JsonDocument.Parse(await revisited.Content.ReadAsStringAsync());
        var brief = saved.RootElement.GetProperty("brief");
        Assert.Equal(empty ? null : "Show quiet\nmorning light", brief.GetProperty("intent").GetString());
        Assert.Equal(empty ? null : "Portrait", brief.GetProperty("genre").GetString());
        Assert.Equal(empty ? null : "Beginner", brief.GetProperty("experience").GetString());
        Assert.Equal(empty ? null : "Improve separation\nand framing", brief.GetProperty("requestedFeedback").GetString());
    }
}
