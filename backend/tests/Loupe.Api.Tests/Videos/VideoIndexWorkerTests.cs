using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.References;
using Loupe.Application.Videos;
using Loupe.Worker;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.Videos;

// L2-057.1, L2-057.2, L2-057.3: the worker embeds saved and edited videos without notes, tolerates missing configuration and provider failures, and re-embeds after a model change.
public sealed class VideoIndexWorkerTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Given_embeddings_configured_when_the_worker_runs_then_saved_and_edited_videos_become_indexed_without_sending_notes()
    {
        var submitted = new List<string>();
        using var ai = new ControlledSourceTransport(async (request, token) => { submitted.Add(await request.Content!.ReadAsStringAsync(token)); return Embedding(0.5f); });
        await using var factory = Factory(ai); using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var saved = await VideoFixture.SaveOk(owner, new
        {
            title = "Posing hands",
            url = "https://youtu.be/" + Guid.NewGuid().ToString("N")[..11],
            topic = "posing",
            channel = "Studio Notes",
            summary = "Where hands go.",
            notes = "PRIVATE takeaway",
            tags = new[] { new { name = "hands", category = "technique" } }
        });
        var id = saved.GetProperty("id").GetGuid(); Assert.False(saved.GetProperty("indexed").GetBoolean());
        await ProcessAsync(factory, owner, id);
        var request = JsonDocument.Parse(Assert.Single(submitted, body => body.Contains("Posing hands"))).RootElement;
        Assert.Equal("embed-fixture", request.GetProperty("model").GetString()); Assert.Equal(1536, request.GetProperty("dimensions").GetInt32());
        var text = request.GetProperty("input").GetString()!;
        Assert.Contains("Posing hands", text); Assert.Contains("posing", text); Assert.Contains("Studio Notes", text); Assert.Contains("Where hands go.", text); Assert.Contains("hands", text);
        Assert.DoesNotContain("PRIVATE", text);
        Assert.All(ai.Requests, uri => Assert.Equal(new Uri("https://loupe-fixture.openai.azure.com/openai/v1/embeddings"), uri));
        using var edited = await owner.PutAsJsonAsync($"/api/videos/{id}", new { revision = 1, title = "Posing feet", url = saved.GetProperty("url").GetString(), topic = "posing" });
        Assert.False((await edited.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("indexed").GetBoolean());
        Assert.True(await Run(factory) >= 1); Assert.Equal(0, await Run(factory));
        Assert.True((await owner.GetFromJsonAsync<JsonElement>($"/api/videos/{id}")).GetProperty("indexed").GetBoolean());
        Assert.Single(submitted, body => body.Contains("Posing feet"));
    }

    [Fact]
    public async Task Given_embeddings_not_configured_when_the_worker_runs_then_videos_stay_readable_and_unindexed_without_provider_calls()
    {
        using var ai = new ControlledSourceTransport((_, _) => throw new InvalidOperationException("Unconfigured embeddings must not call the provider."));
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot) { AiTransport = ai, Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only" } };
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await VideoFixture.SaveOk(owner, new { title = "Unindexed", url = "https://youtu.be/" + Guid.NewGuid().ToString("N")[..11], topic = "gear" })).GetProperty("id").GetGuid();
        Assert.Equal(0, await Run(factory)); Assert.Empty(ai.Requests);
        Assert.False((await owner.GetFromJsonAsync<JsonElement>($"/api/videos/{id}")).GetProperty("indexed").GetBoolean());
    }

    [Fact]
    public async Task Given_a_failing_provider_when_the_worker_runs_then_the_video_is_retried_on_the_next_run()
    {
        var failing = true;
        using var ai = new ControlledSourceTransport((_, _) => Task.FromResult(failing ? new HttpResponseMessage(HttpStatusCode.InternalServerError) : Embedding(0.25f)));
        await using var factory = Factory(ai); using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await VideoFixture.SaveOk(owner, new { title = "Retry me", url = "https://youtu.be/" + Guid.NewGuid().ToString("N")[..11], topic = "editing" })).GetProperty("id").GetGuid();
        Assert.Equal(0, await Run(factory)); Assert.NotEmpty(ai.Requests);
        Assert.False((await owner.GetFromJsonAsync<JsonElement>($"/api/videos/{id}")).GetProperty("indexed").GetBoolean());
        failing = false;
        Assert.True(await Run(factory) >= 1);
        Assert.True((await owner.GetFromJsonAsync<JsonElement>($"/api/videos/{id}")).GetProperty("indexed").GetBoolean());
    }

    [Fact]
    public async Task Given_a_changed_embedding_model_when_the_worker_runs_then_indexed_videos_are_re_embedded_first()
    {
        var subject = Guid.NewGuid().ToString(); Guid id;
        using var ai = new ControlledSourceTransport((_, _) => Task.FromResult(Embedding(0.75f)));
        await using (var factory = Factory(ai))
        {
            using var owner = await factory.CreateAuthenticatedClientAsync(subject);
            id = (await VideoFixture.SaveOk(owner, new { title = "Model change", url = "https://youtu.be/" + Guid.NewGuid().ToString("N")[..11], topic = "interview" })).GetProperty("id").GetGuid();
            Assert.True(await Run(factory) >= 1);
            Assert.True((await owner.GetFromJsonAsync<JsonElement>($"/api/videos/{id}")).GetProperty("indexed").GetBoolean());
        }
        await using var upgraded = Factory(ai, "text-embedding-4"); using var later = await upgraded.CreateAuthenticatedClientAsync(subject);
        Assert.False((await later.GetFromJsonAsync<JsonElement>($"/api/videos/{id}")).GetProperty("indexed").GetBoolean());
        var before = ai.Requests.Count; Assert.True(await Run(upgraded) >= 1); Assert.True(ai.Requests.Count > before);
        Assert.True((await later.GetFromJsonAsync<JsonElement>($"/api/videos/{id}")).GetProperty("indexed").GetBoolean());
    }

    private ApiFactory Factory(HttpMessageHandler ai, string model = "text-embedding-3-small") => new(database.ConnectionString, database.MediaRoot)
    {
        AiTransport = ai,
        Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only", ["Ai:EmbeddingDeployment"] = "embed-fixture", ["Ai:EmbeddingModel"] = model }
    };

    public static HttpResponseMessage Embedding(float value, int dimensions = 1536) => new(HttpStatusCode.OK)
    { Content = JsonContent.Create(new { data = new[] { new { embedding = Enumerable.Repeat(value, dimensions).ToArray() } } }) };

    private static async Task<int> Run(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<ISender>().Send(new IndexVideosCommand())).Indexed;
    }

    private static async Task ProcessAsync(ApiFactory factory, HttpClient owner, Guid id)
    {
        using var worker = ActivatorUtilities.CreateInstance<VideoIndexWorker>(factory.Services); await worker.StartAsync(default);
        try
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                if ((await owner.GetFromJsonAsync<JsonElement>($"/api/videos/{id}")).GetProperty("indexed").GetBoolean()) return;
                await Task.Delay(100);
            }
            Assert.Fail("The index worker did not embed the video.");
        }
        finally { await worker.StopAsync(default); }
    }
}
