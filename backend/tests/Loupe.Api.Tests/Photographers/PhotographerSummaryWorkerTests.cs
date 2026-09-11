using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Loupe.Api.Tests.References;
using Loupe.Worker;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.Photographers;

public sealed class PhotographerSummaryWorkerTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Worker_publishes_grounded_reviewable_suggestions_without_changing_manual_fields(bool insufficient)
    {
        string? submitted = null;
        using var pages = new ControlledSourceTransport((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath == "/robots.txt"
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(insufficient ? "<title>Casey</title>" : "<title>Casey</title><main>Casey makes window-light portraits.<a href='/gallery'>Gallery</a></main>", Encoding.UTF8, "text/html") }));
        using var ai = new ControlledSourceTransport(async (request, token) =>
        {
            submitted = await request.Content!.ReadAsStringAsync(token);
            return Output(insufficient ? "{\"summary\":null,\"tags\":[],\"unavailableReason\":\"insufficient_information\"}"
                : "{\"summary\":\"Casey makes window-light portraits.\",\"tags\":[{\"name\":\"portrait\",\"category\":\"genre\"}],\"unavailableReason\":null}");
        });
        await using var factory = Factory(pages, ai); using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = await SaveAndAdmit(owner); var operation = await Process(factory, owner, id);
        Assert.Equal("Succeeded", operation.GetProperty("status").GetString());
        var suggestions = await owner.GetFromJsonAsync<JsonElement>($"/api/photographers/{id}/suggestions");
        Assert.Equal("Live", suggestions.GetProperty("mode").GetString()); Assert.Equal(1, suggestions.GetProperty("sourceRevision").GetInt64());
        Assert.Equal("https://portfolio.example/", suggestions.GetProperty("source").GetProperty("fetchedUrl").GetString());
        Assert.Equal(insufficient ? "insufficient_information" : null, suggestions.GetProperty("unavailableReason").GetString());
        if (!insufficient) { Assert.Equal("pending", suggestions.GetProperty("summaryStatus").GetString()); Assert.Single(suggestions.GetProperty("tags").EnumerateArray()); }
        Assert.NotNull(submitted); Assert.DoesNotContain("Private notes", submitted); Assert.DoesNotContain("Manual summary", submitted);
        Assert.False(JsonDocument.Parse(submitted).RootElement.GetProperty("store").GetBoolean());
        Assert.DoesNotContain(pages.Requests, uri => uri.AbsolutePath == "/gallery");
        var bookmark = await owner.GetFromJsonAsync<JsonElement>($"/api/photographers/{id}");
        Assert.Equal("Manual summary", bookmark.GetProperty("summary").GetString()); Assert.Equal("Private notes", bookmark.GetProperty("notes").GetString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString()); using var hidden = await stranger.GetAsync($"/api/photographers/{id}/suggestions"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    [Fact]
    public async Task Blocked_source_reports_summary_unavailable_without_calling_the_model()
    {
        using var pages = new ControlledSourceTransport((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("User-agent: Loupe\nDisallow: /\n") }));
        using var ai = new ControlledSourceTransport((_, _) => throw new InvalidOperationException("Blocked sources must not be summarized."));
        await using var factory = Factory(pages, ai); using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = await SaveAndAdmit(owner); var operation = await Process(factory, owner, id);
        Assert.Equal("Failed", operation.GetProperty("status").GetString()); Assert.Equal("robots_disallowed", operation.GetProperty("failureCode").GetString()); Assert.Empty(ai.Requests);
        var bookmark = await owner.GetFromJsonAsync<JsonElement>($"/api/photographers/{id}"); Assert.Equal("Manual summary", bookmark.GetProperty("summary").GetString());
    }

    private ApiFactory Factory(HttpMessageHandler pages, HttpMessageHandler ai) => new(database.ConnectionString, database.MediaRoot)
    { SourceTransport = pages, AiTransport = ai, Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only" } };
    private static HttpResponseMessage Output(string text) => new(HttpStatusCode.OK)
    { Content = JsonContent.Create(new { status = "completed", output = new[] { new { type = "message", content = new[] { new { type = "output_text", text } } } } }) };
    private static async Task<Guid> SaveAndAdmit(HttpClient owner)
    {
        using var save = new HttpRequestMessage(HttpMethod.Post, "/api/photographers") { Content = JsonContent.Create(new { name = "Casey", portfolioUrl = "https://portfolio.example/", summary = "Manual summary", notes = "Private notes" }) };
        save.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString()); using var saved = await owner.SendAsync(save); saved.EnsureSuccessStatusCode();
        var id = (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("photographer").GetProperty("id").GetGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographers/{id}/summary-analysis") { Content = JsonContent.Create(new { revision = 1 }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString()); using var admitted = await owner.SendAsync(request); admitted.EnsureSuccessStatusCode(); return id;
    }
    private static async Task<JsonElement> Process(ApiFactory factory, HttpClient owner, Guid id)
    {
        using var worker = ActivatorUtilities.CreateInstance<AnalysisWorker>(factory.Services); await worker.StartAsync(default);
        try
        {
            JsonElement operation = default;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                operation = await owner.GetFromJsonAsync<JsonElement>($"/api/photographers/{id}/summary-analysis");
                if (operation.GetProperty("status").GetString() is "Succeeded" or "Failed") return operation;
                await Task.Delay(100);
            }
            return operation;
        }
        finally { await worker.StopAsync(default); }
    }
}
