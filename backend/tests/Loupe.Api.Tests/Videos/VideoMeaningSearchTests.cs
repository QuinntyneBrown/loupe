using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.References;
using Loupe.Application.Videos;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.Videos;

// L2-058.1, L2-058.2, L2-058.3: meaning search ranks indexed videos by cosine similarity with filters and the 0.20 threshold, rejects blank queries, and reports unavailability.
public sealed class VideoMeaningSearchTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Given_indexed_videos_when_searching_by_meaning_then_eligible_videos_rank_by_similarity_with_filters_and_scores()
    {
        using var ai = new ControlledSourceTransport(async (request, token) => Embedding(await request.Content!.ReadAsStringAsync(token)));
        await using var factory = Factory(ai); using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var hands = await Save(owner, "Posing hands in portraits", "posing", "hands");
        var handsToo = await Save(owner, "More hands posing", "posing", "hands");
        var light = await Save(owner, "One light portrait setups", "lighting", "window light");
        var interview = await Save(owner, "Interview with a master", "interview");
        var stale = await Save(owner, "Hands before an edit", "posing");
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        await Save(stranger, "Hands of a stranger", "posing");
        await Run(factory);
        using var edited = await owner.PutAsJsonAsync($"/api/videos/{stale}", new { revision = 1, title = "Hands after an edit", url = "https://youtu.be/" + Guid.NewGuid().ToString("N")[..11], topic = "posing" });
        edited.EnsureSuccessStatusCode();
        var page = await VideoFixture.List(owner, "mode=meaning&query=hand%20posing%20with%20a%20little%20light");
        var expected = new[] { hands, handsToo }.Order().Append(light).ToArray();
        Assert.Equal(expected, VideoFixture.Ids(page)); Assert.Equal(3, page.GetProperty("totalCount").GetInt32());
        var scores = page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("score").GetDouble()).ToArray();
        Assert.True(scores[0] > 0.9 && scores[0] == scores[1] && scores[2] is > 0.2 and < 0.5, string.Join(",", scores));
        Assert.Equal(JsonValueKind.Null, page.GetProperty("nextCursor").ValueKind);
        Assert.DoesNotContain(interview, VideoFixture.Ids(page)); Assert.DoesNotContain(stale, VideoFixture.Ids(page));
        Assert.Equal([light], VideoFixture.Ids(await VideoFixture.List(owner, "mode=meaning&query=hand%20posing%20with%20a%20little%20light&topic=lighting")));
        Assert.Equal([light], VideoFixture.Ids(await VideoFixture.List(owner, "mode=meaning&query=hand%20posing%20with%20a%20little%20light&tags=window%20light")));
        var limited = await VideoFixture.List(owner, "mode=meaning&query=hand%20posing%20with%20a%20little%20light&pageSize=1");
        Assert.Single(VideoFixture.Ids(limited)); Assert.Equal(3, limited.GetProperty("totalCount").GetInt32());
        Assert.Empty(VideoFixture.Ids(await VideoFixture.List(owner, "mode=meaning&query=unrelated")));
        var keyword = await VideoFixture.List(owner, "query=hands");
        Assert.All(keyword.GetProperty("items").EnumerateArray(), item => Assert.Equal(JsonValueKind.Null, item.GetProperty("score").ValueKind));
    }

    [Theory]
    [InlineData("mode=meaning", "query")]
    [InlineData("mode=meaning&query=%20%20", "query")]
    [InlineData("mode=vibes&query=hands", "mode")]
    public async Task Given_a_blank_query_or_unknown_mode_when_searching_then_a_field_error_is_returned_without_embedding(string query, string field)
    {
        using var ai = new ControlledSourceTransport((_, _) => throw new InvalidOperationException("Invalid searches must not embed."));
        await using var factory = Factory(ai); using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var response = await owner.GetAsync("/api/videos?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotEmpty((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty(field).EnumerateArray());
        Assert.Empty(ai.Requests);
    }

    [Theory]
    [InlineData(false, "integration_not_configured")]
    [InlineData(true, "service_unavailable")]
    public async Task Given_unconfigured_or_failing_embeddings_when_searching_by_meaning_then_unavailability_is_reported(bool configured, string code)
    {
        using var ai = new ControlledSourceTransport((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        await using var factory = configured ? Factory(ai) : new ApiFactory(database.ConnectionString, database.MediaRoot) { AiTransport = ai, Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only" } };
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var response = await owner.GetAsync("/api/videos?mode=meaning&query=hands");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(0, (await VideoFixture.List(owner, "query=hands")).GetProperty("totalCount").GetInt32());
    }

    private ApiFactory Factory(HttpMessageHandler ai) => new(database.ConnectionString, database.MediaRoot)
    {
        AiTransport = ai,
        Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only", ["Ai:EmbeddingDeployment"] = "embed-fixture" }
    };

    private static async Task<Guid> Save(HttpClient owner, string title, string topic, string? tag = null) =>
        (await VideoFixture.SaveOk(owner, new { title, url = "https://youtu.be/" + Guid.NewGuid().ToString("N")[..11], topic, tags = tag is null ? [] : new[] { new { name = tag } } })).GetProperty("id").GetGuid();

    private static async Task Run(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new IndexVideosCommand(100));
    }

    // A controlled embedding: axis 0 for hands, axis 1 for light, axis 2 for interviews, weighted by the words present.
    private static HttpResponseMessage Embedding(string body)
    {
        var text = JsonDocument.Parse(body).RootElement.GetProperty("input").GetString()!.ToLowerInvariant();
        var vector = new float[1536];
        vector[0] = text.Contains("hand") ? 1 : 0;
        vector[1] = text.Contains("light") ? (text.Contains("little") ? 0.3f : 1) : 0;
        vector[2] = text.Contains("interview") ? 1 : 0;
        vector[3] = text.Contains("unrelated") ? 1 : 0;
        return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { data = new[] { new { embedding = vector } } }) };
    }
}
