using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.Photographers;

public sealed class PhotographerDraftTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Reading_creates_only_a_private_draft_and_cancel_is_safe_to_retry()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot) { Settings = new Dictionary<string,string?> { ["Imports:Mode"] = "Live" } };
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var key = Guid.NewGuid().ToString();
        using var response = await Read(owner, "https://portfolio.example/", key); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var draft = await response.Content.ReadFromJsonAsync<JsonElement>(); var id = draft.GetProperty("id").GetGuid();
        Assert.Equal("Queued", draft.GetProperty("import").GetProperty("status").GetString());
        using var repeated = await Read(owner, "https://portfolio.example/", key); repeated.EnsureSuccessStatusCode();
        Assert.Equal(id, (await repeated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        using var hidden = await stranger.GetAsync($"/api/photographer-drafts/{id}"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        var library = await owner.GetFromJsonAsync<JsonElement>("/api/photographers"); Assert.Empty(library.GetProperty("items").EnumerateArray());
        for (var attempt = 0; attempt < 2; attempt++) {using var cancel = await owner.DeleteAsync($"/api/photographer-drafts/{id}"); Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);}
        using var removed = await owner.GetAsync($"/api/photographer-drafts/{id}"); Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
        using var canceledReplay = await Read(owner, "https://portfolio.example/", key); Assert.Equal(HttpStatusCode.NotFound, canceledReplay.StatusCode);
    }

    [Fact]
    public async Task An_existing_normalized_portfolio_is_reported_without_an_import_job()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot) { Settings = new Dictionary<string,string?> { ["Imports:Mode"] = "Live" } };
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var request = new HttpRequestMessage(HttpMethod.Post,"/api/photographers") {Content=JsonContent.Create(new {name="Casey",portfolioUrl="https://portfolio.example/work"})}; request.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString());
        using var saved = await owner.SendAsync(request); saved.EnsureSuccessStatusCode();
        var id = (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("photographer").GetProperty("id").GetGuid();
        using var response = await Read(owner,"HTTPS://PORTFOLIO.EXAMPLE:443/work#about",Guid.NewGuid().ToString()); Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var draft = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(id,draft.GetProperty("committedPhotographerId").GetGuid()); Assert.Equal(JsonValueKind.Null,draft.GetProperty("import").ValueKind); Assert.Equal("Casey",draft.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Unconfigured_reading_offers_manual_fallback_and_drafts_expire_after_twenty_four_hours()
    {
        await using var factory = new ApiFactory(database.ConnectionString,database.MediaRoot) {Settings=new Dictionary<string,string?> {["Imports:Mode"]=null}};
        var subject=Guid.NewGuid().ToString(); using var owner=await factory.CreateAuthenticatedClientAsync(subject);
        using var response=await Read(owner,"https://portfolio.example/",Guid.NewGuid().ToString()); Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var draft=await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal("integration_not_configured",draft.GetProperty("failureCode").GetString());
        factory.Clock.Advance(TimeSpan.FromHours(25)); using var fresh=await factory.CreateAuthenticatedClientAsync(subject);
        using var expired=await fresh.GetAsync($"/api/photographer-drafts/{draft.GetProperty("id").GetGuid()}"); Assert.Equal(HttpStatusCode.NotFound,expired.StatusCode);
    }

    private static async Task<HttpResponseMessage> Read(HttpClient owner,string url,string key)
    {
        using var request=new HttpRequestMessage(HttpMethod.Post,"/api/photographer-drafts") {Content=JsonContent.Create(new {portfolioUrl=url})}; request.Headers.Add("Idempotency-Key",key); return await owner.SendAsync(request);
    }
}
