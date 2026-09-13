using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.Videos;

// L2-056.1, L2-056.2, L2-056.3: the video library lists newest first with stable paging, keyword matching, topic and tag filters, and tag facets.
public sealed class ListVideoTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Given_25_owned_videos_when_paged_then_each_appears_once_newest_first_and_strangers_see_none()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var ids = new List<Guid>();
        for (var index = 0; index < 25; index++)
        {
            factory.Clock.Advance(TimeSpan.FromSeconds(1));
            ids.Add((await VideoFixture.SaveOk(owner, new { title = $"Video {index:00}", url = "https://youtu.be/" + Guid.NewGuid().ToString("N")[..11], topic = "posing" })).GetProperty("id").GetGuid());
        }
        var first = await VideoFixture.List(owner);
        Assert.Equal(25, first.GetProperty("totalCount").GetInt32());
        Assert.Equal(ids.AsEnumerable().Reverse().Take(24), VideoFixture.Ids(first));
        var second = await VideoFixture.List(owner, "cursor=" + Uri.EscapeDataString(first.GetProperty("nextCursor").GetString()!));
        Assert.Equal([ids[0]], VideoFixture.Ids(second));
        Assert.Equal(JsonValueKind.Null, second.GetProperty("nextCursor").ValueKind);
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        Assert.Equal(0, (await VideoFixture.List(stranger)).GetProperty("totalCount").GetInt32());
        using var foreignCursor = await stranger.GetAsync("/api/videos?cursor=" + Uri.EscapeDataString(first.GetProperty("nextCursor").GetString()!));
        Assert.Equal(HttpStatusCode.BadRequest, foreignCursor.StatusCode);
    }

    [Fact]
    public async Task Given_keyword_topic_and_tag_filters_when_listing_then_only_matching_videos_and_accurate_facets_are_returned()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var posing = (await VideoFixture.SaveOk(owner, new
        {
            title = "Posing couples",
            url = "https://youtu.be/" + Guid.NewGuid().ToString("N")[..11],
            topic = "posing",
            channel = "Studio Notes",
            tags = new[] { new { name = "Couples", category = "subject" }, new { name = "hands", category = "technique" } }
        })).GetProperty("id").GetGuid();
        factory.Clock.Advance(TimeSpan.FromSeconds(1));
        var lighting = (await VideoFixture.SaveOk(owner, new
        {
            title = "One light setups",
            url = "https://youtu.be/" + Guid.NewGuid().ToString("N")[..11],
            topic = "lighting",
            summary = "Rembrandt and butterfly patterns.",
            notes = "Try the clamshell idea",
            tags = new[] { new { name = "couples", category = (string?)null } }
        })).GetProperty("id").GetGuid();
        factory.Clock.Advance(TimeSpan.FromSeconds(1));
        var interview = (await VideoFixture.SaveOk(owner, new { title = "Interview: a legend on seeing", url = "https://youtu.be/" + Guid.NewGuid().ToString("N")[..11], topic = "interview" })).GetProperty("id").GetGuid();
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        await VideoFixture.SaveOk(stranger, new { title = "Posing strangers", url = "https://youtu.be/" + Guid.NewGuid().ToString("N")[..11], topic = "posing", tags = new[] { new { name = "couples" } } });
        Assert.Equal([lighting], VideoFixture.Ids(await VideoFixture.List(owner, "query=REMBRANDT")));
        Assert.Equal([lighting], VideoFixture.Ids(await VideoFixture.List(owner, "query=clamshell")));
        Assert.Equal([posing], VideoFixture.Ids(await VideoFixture.List(owner, "query=studio%20notes")));
        Assert.Equal([lighting, posing], VideoFixture.Ids(await VideoFixture.List(owner, "query=couples")));
        Assert.Equal([posing], VideoFixture.Ids(await VideoFixture.List(owner, "query=couples&topic=posing")));
        Assert.Equal([interview], VideoFixture.Ids(await VideoFixture.List(owner, "topic=interview")));
        Assert.Equal([lighting, posing], VideoFixture.Ids(await VideoFixture.List(owner, "tags=COUPLES")));
        var handsPage = await VideoFixture.List(owner, "tags=couples&tags=hands");
        Assert.Equal([posing], VideoFixture.Ids(handsPage)); Assert.Equal(1, handsPage.GetProperty("totalCount").GetInt32());
        Assert.Empty(VideoFixture.Ids(await VideoFixture.List(owner, "query=zzz")));
        using var badTopic = await owner.GetAsync("/api/videos?topic=dance");
        Assert.Equal(HttpStatusCode.BadRequest, badTopic.StatusCode);
        var facets = await owner.GetFromJsonAsync<JsonElement>("/api/videos/tags");
        Assert.Equal(["Couples", "hands"], facets.EnumerateArray().Select(facet => facet.GetProperty("name").GetString()));
        Assert.Equal([2, 1], facets.EnumerateArray().Select(facet => facet.GetProperty("count").GetInt32()));
    }
}
