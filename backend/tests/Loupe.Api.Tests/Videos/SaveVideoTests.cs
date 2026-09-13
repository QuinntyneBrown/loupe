using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.Videos;

// L2-055.1, L2-055.2, L2-055.3: video bookmarks persist privately with canonical URLs, validate input, and deduplicate video identifiers.
public sealed class SaveVideoTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=42")]
    [InlineData("HTTPS://m.youtube.com/watch?feature=share&v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/live/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ")]
    public async Task Given_a_valid_video_when_saved_then_it_persists_privately_with_a_canonical_url(string url)
    {
        var subject = Guid.NewGuid().ToString(); Guid id;
        await using (var factory = new ApiFactory(database.ConnectionString, database.MediaRoot))
        {
            using var owner = await factory.CreateAuthenticatedClientAsync(subject);
            using var saved = await VideoFixture.Save(owner, new
            {
                title = "  Posing hands in portraits  ",
                url,
                topic = "posing",
                channel = "Studio Notes",
                summary = "Where to put hands.",
                notes = "My private takeaways",
                tags = new object[] { new { name = " hands ", category = "technique" }, new { name = "Hands" } }
            });
            Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
            var result = await saved.Content.ReadFromJsonAsync<JsonElement>(); id = result.GetProperty("id").GetGuid();
            Assert.Equal($"/api/videos/{id}", saved.Headers.Location?.ToString());
            Assert.Equal("Posing hands in portraits", result.GetProperty("title").GetString());
            Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", result.GetProperty("url").GetString());
            Assert.Equal("dQw4w9WgXcQ", result.GetProperty("videoId").GetString());
            Assert.Equal("https://i.ytimg.com/vi/dQw4w9WgXcQ/hqdefault.jpg", result.GetProperty("thumbnailUrl").GetString());
            Assert.Equal("posing", result.GetProperty("topic").GetString());
            Assert.False(result.GetProperty("indexed").GetBoolean());
            Assert.Equal(JsonValueKind.Null, result.GetProperty("score").ValueKind);
            var tag = Assert.Single(result.GetProperty("tags").EnumerateArray());
            Assert.Equal("hands", tag.GetProperty("name").GetString()); Assert.Equal("technique", tag.GetProperty("category").GetString()); Assert.Equal("manual", tag.GetProperty("provenance").GetString());
            using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
            using var hidden = await stranger.GetAsync($"/api/videos/{id}"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        }
        await using var restart = new ApiFactory(database.ConnectionString, database.MediaRoot); using var later = await restart.CreateAuthenticatedClientAsync(subject);
        var video = await later.GetFromJsonAsync<JsonElement>($"/api/videos/{id}");
        Assert.Equal("My private takeaways", video.GetProperty("notes").GetString()); Assert.Equal("Where to put hands.", video.GetProperty("summary").GetString());
        Assert.Equal("Studio Notes", video.GetProperty("channel").GetString()); Assert.Equal(1, video.GetProperty("revision").GetInt64());
    }

    [Fact]
    public async Task Given_a_video_already_saved_when_saved_again_then_a_conflict_names_the_existing_bookmark()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var marker = Guid.NewGuid().ToString("N")[..11];
        var first = await VideoFixture.SaveOk(owner, new { title = "Original", url = "https://www.youtube.com/watch?v=" + marker, topic = "lighting" });
        using var duplicate = await VideoFixture.Save(owner, new { title = "Again", url = "https://youtu.be/" + marker, topic = "posing" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("video_conflict", (await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<LibraryDbContext>().Videos.CountAsync(video => video.VideoId == marker));
        using var other = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var independent = await VideoFixture.SaveOk(other, new { title = "Mine", url = "https://youtu.be/" + marker, topic = "posing" });
        Assert.NotEqual(first.GetProperty("id").GetGuid(), independent.GetProperty("id").GetGuid());
    }

    [Theory]
    [InlineData("title", "")]
    [InlineData("title", "overlength")]
    [InlineData("url", "https://vimeo.com/12345")]
    [InlineData("url", "https://www.youtube.com/@channel")]
    [InlineData("url", "https://www.youtube.com/watch?v=short")]
    [InlineData("url", "javascript:alert(1)")]
    [InlineData("url", "https://user:pw@www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("topic", "dance")]
    [InlineData("topic", "")]
    [InlineData("summary", "overlength")]
    [InlineData("tags", "category")]
    public async Task Given_invalid_input_when_saved_then_a_field_error_is_returned_and_nothing_is_saved(string field, string value)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var marker = Guid.NewGuid().ToString("N")[..11];
        var input = new Dictionary<string, object?> { ["title"] = "Valid " + marker, ["url"] = "https://youtu.be/" + marker, ["topic"] = "posing" };
        input[field] = field == "tags" ? new[] { new { name = "x", category = "dance" } } : value == "overlength" ? new string('x', field == "title" ? 201 : 4001) : value;
        using var result = await VideoFixture.Save(owner, input);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.True((await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty(field, out _));
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<LibraryDbContext>().Videos.AnyAsync(video => video.VideoId == marker || video.Title.Contains(marker)));
    }
}
