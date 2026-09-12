using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Infrastructure.ReferenceImports;
using Loupe.Infrastructure.Ai;
using Loupe.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Loupe.Api.Tests.References;

public sealed class SourceWorkerExecutionTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Independent_worker_discovers_admitted_draft_and_publishes_fallback()
    {
        var settings = new Dictionary<string, string?> { ["Imports:Mode"] = "Live" };
        await using var api = new ApiFactory(database.ConnectionString, database.MediaRoot) { Settings = settings };
        using var client = await api.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/reference-drafts/links") { Content = JsonContent.Create(new { sourceUrl = "https://source.example/work" }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var admitted = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        var draft = await admitted.Content.ReadFromJsonAsync<JsonElement>();
        using var transport = new ControlledSourceTransport((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("User-agent: *\nDisallow: /\n") }));
        await using var host = new ApiFactory(database.ConnectionString, database.MediaRoot) { SourceTransport = transport, Settings = settings };
        using var worker = new ReferenceImportWorker(host.Services.GetRequiredService<IServiceScopeFactory>(),
            host.Services.GetRequiredService<IOptions<ReferenceImportOptions>>(), host.Services.GetRequiredService<IOptions<AiOptions>>(), NullLogger<ReferenceImportWorker>.Instance);
        await worker.StartAsync(default);
        JsonElement result = default;
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            do
            {
                result = await client.GetFromJsonAsync<JsonElement>($"/api/reference-drafts/{draft.GetProperty("id").GetGuid()}");
                if (result.GetProperty("import").GetProperty("status").GetString() == "Failed") break;
                await Task.Delay(100);
            } while (DateTime.UtcNow < deadline);
        }
        finally { await worker.StopAsync(default); }
        Assert.Equal("robots_disallowed", result.GetProperty("failureCode").GetString());
    }
}
