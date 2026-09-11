using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using Loupe.Application.ReferenceImports;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.References;

// Given an admitted private draft, a permitted source produces an editable preview;
// the worker never saves a reference, and source restrictions produce a manual fallback.
public sealed class SourceDraftWorkerTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData("og:image")]
    [InlineData("twitter:image")]
    [InlineData("direct")]
    public async Task Permitted_images_produce_a_private_preview_without_committing(string imageKind)
    {
        using var image = NetVips.Image.Black(8, 6, bands: 3);
        var png = image.PngsaveBuffer();
        using var transport = new ControlledSourceTransport((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/robots.txt") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            if (imageKind == "direct" || request.RequestUri.AbsolutePath == "/photo.png")
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(png) };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                return Task.FromResult(response);
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent($"<title>Light &amp; shade</title><meta name='author' content='Casey Example'><meta property='{imageKind}' content='/photo.png'>", System.Text.Encoding.UTF8, "text/html") });
        });
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var draft = await Start(owner);
        await Run(factory);
        var result = await owner.GetFromJsonAsync<JsonElement>($"/api/reference-drafts/{draft.GetProperty("id").GetGuid()}");
        Assert.Equal("Succeeded", result.GetProperty("import").GetProperty("status").GetString());
        Assert.Equal(8, result.GetProperty("width").GetInt32());
        if (imageKind != "direct")
        {
            Assert.Equal("Light & shade", result.GetProperty("title").GetString());
            Assert.Equal("Casey Example", result.GetProperty("attribution").GetString());
        }
        else Assert.Equal(JsonValueKind.Null, result.GetProperty("attribution").ValueKind);
        using var preview = await owner.GetAsync(result.GetProperty("previewUrl").GetString());
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var library = await owner.GetFromJsonAsync<JsonElement>("/api/references");
        Assert.Empty(library.GetProperty("items").EnumerateArray());
        Assert.Equal("/robots.txt", transport.Requests[0].AbsolutePath);
    }

    [Fact]
    public async Task Disallowed_source_is_never_read_and_leaves_a_manual_fallback()
    {
        using var transport = new ControlledSourceTransport((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("User-agent: Loupe\nDisallow: /\n") }));
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var draft = await Start(owner);
        await Run(factory);
        var result = await owner.GetFromJsonAsync<JsonElement>($"/api/reference-drafts/{draft.GetProperty("id").GetGuid()}");
        Assert.Equal("Failed", result.GetProperty("import").GetProperty("status").GetString());
        Assert.Equal("robots_disallowed", result.GetProperty("failureCode").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("previewUrl").ValueKind);
        Assert.Equal("/robots.txt", Assert.Single(transport.Requests).AbsolutePath);
    }

    private ApiFactory Factory(HttpMessageHandler transport) => new(database.ConnectionString, database.MediaRoot)
    { SourceTransport = transport, Settings = new Dictionary<string, string?> { ["Imports:Mode"] = "Live" } };

    [Fact]
    public async Task Cancel_during_fetch_prevents_late_preview_publication()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var transport = new ControlledSourceTransport(async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath == "/robots.txt") return new HttpResponseMessage(HttpStatusCode.NotFound);
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<title>Late title</title>", System.Text.Encoding.UTF8, "text/html") };
        });
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var draft = await Start(owner);
        var running = Run(factory);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var canceled = await owner.DeleteAsync($"/api/reference-drafts/{draft.GetProperty("id").GetGuid()}");
            Assert.Equal(HttpStatusCode.NoContent, canceled.StatusCode);
        }
        finally { release.TrySetResult(); }
        await running;
        using var missing = await owner.GetAsync($"/api/reference-drafts/{draft.GetProperty("id").GetGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var operation = await owner.GetFromJsonAsync<JsonElement>($"/api/operations/{draft.GetProperty("import").GetProperty("id").GetGuid()}");
        Assert.Equal("Canceled", operation.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Redirect_target_policy_is_checked_before_reading_that_page()
    {
        using var transport = new ControlledSourceTransport((request, _) =>
        {
            if (request.RequestUri!.Host == "denied.example") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("User-agent: *\nDisallow: /\n") });
            if (request.RequestUri.AbsolutePath == "/robots.txt") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            var redirect = new HttpResponseMessage(HttpStatusCode.Found);
            redirect.Headers.Location = new Uri("https://denied.example/private");
            return Task.FromResult(redirect);
        });
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var draft = await Start(owner);
        await Run(factory);
        var result = await owner.GetFromJsonAsync<JsonElement>($"/api/reference-drafts/{draft.GetProperty("id").GetGuid()}");
        Assert.Equal("robots_disallowed", result.GetProperty("failureCode").GetString());
        Assert.DoesNotContain(transport.Requests, uri => uri.AbsolutePath == "/private");
        Assert.Contains(transport.Requests, uri => uri.Host == "denied.example" && uri.AbsolutePath == "/robots.txt");
    }

    [Fact]
    public async Task Ordinary_page_images_are_not_silently_selected_as_preview()
    {
        using var transport = new ControlledSourceTransport((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath == "/robots.txt"
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<title>Keep this title</title><img src='/arbitrary.png'>", System.Text.Encoding.UTF8, "text/html") }));
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var draft = await Start(owner);
        await Run(factory);
        var result = await owner.GetFromJsonAsync<JsonElement>($"/api/reference-drafts/{draft.GetProperty("id").GetGuid()}");
        Assert.Equal("image_not_found", result.GetProperty("failureCode").GetString());
        Assert.Equal("Keep this title", result.GetProperty("title").GetString());
        Assert.DoesNotContain(transport.Requests, uri => uri.AbsolutePath == "/arbitrary.png");
    }

    private static async Task<JsonElement> Start(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/reference-drafts/links") { Content = JsonContent.Create(new { sourceUrl = "https://source.example/work" }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task Run(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunReferenceImportCommand()));
    }
}
