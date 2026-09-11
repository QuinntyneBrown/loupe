using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Application.ReferenceAnalysis;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Loupe.Infrastructure.Ai;
using Loupe.Worker;
using Xunit;

namespace Loupe.Api.Tests.References;

public sealed class ReferenceAnalysisWorkerTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    // Given queued visual analysis, when the AI host runs, then it publishes without an API request running the job.
    [Fact]
    public async Task Background_host_drains_reference_analysis()
    {
        using var transport = new ControlledSourceTransport((_, _) => Task.FromResult(Output("{\"description\":\"A quiet room.\",\"tags\":[]}")));
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var upload = await ReferenceFixture.SubmitAsync(owner, new Dictionary<string, string>());
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var admitted = await AdmitAsync(owner, id);
        using var host = new AnalysisWorker(factory.Services.GetRequiredService<IServiceScopeFactory>(),
            factory.Services.GetRequiredService<IOptions<AiOptions>>(), NullLogger<AnalysisWorker>.Instance);
        await host.StartAsync(default);
        try
        {
            for (var attempt = 0; attempt < 50; attempt++)
            {
                var operation = await owner.GetFromJsonAsync<JsonElement>(admitted.Headers.Location);
                if (operation.GetProperty("status").GetString() == "Succeeded") return;
                await Task.Delay(100);
            }
            Assert.Fail("The background host did not publish queued reference suggestions.");
        }
        finally { await host.StopAsync(default); }
    }

    [Theory]
    [InlineData("{\"description\":\"\",\"tags\":[]}")]
    [InlineData("{\"description\":\"A room\",\"tags\":[{\"name\":\"invented\",\"category\":\"camera\"}]}")]
    [InlineData("{\"description\":\"A room\",\"tags\":[{\"name\":\"soft light\",\"category\":\"lighting\"},{\"name\":\"SOFT LIGHT\",\"category\":\"lighting\"}]}")]
    [InlineData("{\"description\":\"A room\"}")]
    public async Task Invalid_output_retries_once_without_publishing_partial_suggestions(string output)
    {
        using var transport = new ControlledSourceTransport((_, _) => Task.FromResult(Output(output)));
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var upload = await ReferenceFixture.SubmitAsync(owner, new Dictionary<string, string>());
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var admitted = await AdmitAsync(owner, id);
        Assert.True(await RunAsync(factory));
        var queued = await owner.GetFromJsonAsync<JsonElement>(admitted.Headers.Location);
        Assert.Equal("Queued", queued.GetProperty("status").GetString());
        using var empty = await owner.GetAsync($"/api/references/{id}/suggestions");
        Assert.Equal(HttpStatusCode.NoContent, empty.StatusCode);
        Assert.False(await RunAsync(factory));
        factory.Clock.Advance(TimeSpan.FromSeconds(5));
        Assert.True(await RunAsync(factory));
        var failed = await owner.GetFromJsonAsync<JsonElement>(admitted.Headers.Location);
        Assert.Equal("Failed", failed.GetProperty("status").GetString());
        Assert.Equal("invalid_output", failed.GetProperty("failureCode").GetString());
        Assert.False(await RunAsync(factory));
        Assert.Equal(2, transport.Requests.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_output_cannot_publish_after_replacement_or_deletion(bool delete)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var transport = new ControlledSourceTransport(async (_, _) =>
        {
            started.TrySetResult();
            await release.Task;
            return Output("{\"description\":\"The old image\",\"tags\":[]}");
        });
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var upload = await ReferenceFixture.SubmitAsync(owner, new Dictionary<string, string>());
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var admitted = await AdmitAsync(owner, id);
        var running = RunAsync(factory);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var mutation = delete ? await owner.DeleteAsync($"/api/references/{id}?revision=1")
                : await ReferenceFixture.SubmitAsync(owner, new Dictionary<string, string> { ["revision"] = "1" }, path: $"/api/references/{id}/image", method: HttpMethod.Put);
            mutation.EnsureSuccessStatusCode();
        }
        finally { release.TrySetResult(); }
        Assert.True(await running);
        var canceled = await owner.GetFromJsonAsync<JsonElement>(admitted.Headers.Location);
        Assert.Equal("Canceled", canceled.GetProperty("status").GetString());
        using var suggestions = await owner.GetAsync($"/api/references/{id}/suggestions");
        Assert.Equal(delete ? HttpStatusCode.NotFound : HttpStatusCode.NoContent, suggestions.StatusCode);
    }

    private ApiFactory Factory(HttpMessageHandler transport) => new(database.ConnectionString, database.MediaRoot)
    { AiTransport = transport, Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only" } };

    private static HttpResponseMessage Output(string text) => new(HttpStatusCode.OK)
    { Content = JsonContent.Create(new { status = "completed", output = new[] { new { type = "message", content = new[] { new { type = "output_text", text } } } } }) };

    private static async Task<HttpResponseMessage> AdmitAsync(HttpClient client, Guid id)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/references/{id}/analysis") { Content = JsonContent.Create(new { revision = 1 }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private static async Task<bool> RunAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunReferenceAnalysisCommand());
    }

    [Fact]
    public async Task Valid_visual_output_is_private_unreviewed_and_does_not_overwrite_editorial_content()
    {
        using var transport = new ControlledSourceTransport((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { status = "completed", output = new[] { new { type = "message", content = new[] { new { type = "output_text", text = "{\"description\":\"Soft window light across a quiet room.\",\"tags\":[{\"name\":\"soft light\",\"category\":\"lighting\"}]}" } } } } })
        }));
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot)
        { AiTransport = transport, Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only" } };
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var upload = await ReferenceFixture.SubmitAsync(owner, new Dictionary<string, string> { ["notes"] = "My private notes" });
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var description = await owner.PutAsJsonAsync($"/api/references/{id}/description", new { revision = 1, text = "My description" }); description.EnsureSuccessStatusCode();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/references/{id}/analysis") { Content = JsonContent.Create(new { revision = 2 }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString()); using var admitted = await owner.SendAsync(request); admitted.EnsureSuccessStatusCode();
        await using var worker = factory.Services.CreateAsyncScope();
        Assert.True(await worker.ServiceProvider.GetRequiredService<ISender>().Send(new RunReferenceAnalysisCommand()));
        var operation = await owner.GetFromJsonAsync<JsonElement>(admitted.Headers.Location); Assert.Equal("Succeeded", operation.GetProperty("status").GetString());
        var suggestions = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{id}/suggestions");
        Assert.Equal("Soft window light across a quiet room.", suggestions.GetProperty("description").GetString());
        Assert.Equal("soft light", Assert.Single(suggestions.GetProperty("tags").EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal("Live", suggestions.GetProperty("mode").GetString());
        var reference = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{id}");
        Assert.Equal("My description", reference.GetProperty("description").GetString()); Assert.Equal("My private notes", reference.GetProperty("notes").GetString());
        Assert.Empty(reference.GetProperty("tags").EnumerateArray());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var hidden = await stranger.GetAsync($"/api/references/{id}/suggestions"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }
}
