using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.References;
using Loupe.Application.PhotographerImports;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.Photographers;

public sealed class SavePhotographerDraftTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Manual_fallback_saves_once_and_validation_or_stale_revisions_do_not_commit()
    {
        await using var factory=new ApiFactory(database.ConnectionString,database.MediaRoot) {Settings=new Dictionary<string,string?> {["Imports:Mode"]=null}};
        using var owner=await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var draft=await Start(owner); var id=draft.GetProperty("id").GetGuid();
        using var invalid=await Save(owner,id,new {name="",portfolioUrl="https://portfolio.example/",revision=1}); Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);
        using var stale=await Save(owner,id,new {name="Casey",portfolioUrl="https://portfolio.example/",revision=2}); Assert.Equal(HttpStatusCode.Conflict,stale.StatusCode);
        var empty=await owner.GetFromJsonAsync<JsonElement>("/api/photographers"); Assert.Empty(empty.GetProperty("items").EnumerateArray());
        var input=new {name=" Casey ",portfolioUrl="https://portfolio.example/",summary="My observation",notes="Private study notes",tags=new[] {new {name="portrait",category="genre"}},revision=1}; var key=Guid.NewGuid().ToString();
        using var first=await Save(owner,id,input,key); Assert.Equal(HttpStatusCode.OK,first.StatusCode);
        var bookmark=(await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("photographer"); Assert.Equal("Casey",bookmark.GetProperty("name").GetString()); Assert.Equal("Private study notes",bookmark.GetProperty("notes").GetString());
        using var repeat=await Save(owner,id,input,key); repeat.EnsureSuccessStatusCode(); Assert.Equal(bookmark.GetProperty("id").GetGuid(),(await repeat.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("photographer").GetProperty("id").GetGuid());
        var collection=await owner.GetFromJsonAsync<JsonElement>("/api/photographers"); Assert.Single(collection.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Captured_source_survives_draft_expiry_and_is_marked_previous_after_a_real_url_change()
    {
        using var transport=new ControlledSourceTransport((request,_)=>Task.FromResult(request.RequestUri!.AbsolutePath=="/robots.txt"?new HttpResponseMessage(HttpStatusCode.NotFound):new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent("<title>Casey</title><meta name='description' content='Portfolio description'><main>Casey photographs interiors.</main>",System.Text.Encoding.UTF8,"text/html")}));
        await using var factory=new ApiFactory(database.ConnectionString,database.MediaRoot) {SourceTransport=transport,Settings=new Dictionary<string,string?> {["Imports:Mode"]="Live"}};
        var subject=Guid.NewGuid().ToString(); using var owner=await factory.CreateAuthenticatedClientAsync(subject);
        var draft=await Start(owner); var id=draft.GetProperty("id").GetGuid();
        using var pending=await Save(owner,id,new {name="Casey",portfolioUrl="https://portfolio.example/",revision=1}); Assert.Equal(HttpStatusCode.Conflict,pending.StatusCode);
        await using (var scope=factory.Services.CreateAsyncScope()) Assert.True(await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunPhotographerImportCommand()));
        using var save=await Save(owner,id,new {name="Edited name",portfolioUrl="https://portfolio.example/",summary="My edited description",notes="Keep my notes",revision=2}); Assert.Equal(HttpStatusCode.OK,save.StatusCode);
        var bookmark=(await save.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("photographer"); var bookmarkId=bookmark.GetProperty("id").GetGuid();
        Assert.True(bookmark.GetProperty("sourceIsCurrent").GetBoolean()); Assert.Equal("Casey photographs interiors.",bookmark.GetProperty("source").GetProperty("mainText").GetString());
        using var equivalent=await owner.PutAsJsonAsync($"/api/photographers/{bookmarkId}",new {name="Edited name",portfolioUrl="HTTPS://PORTFOLIO.EXAMPLE:443/#about",summary="My edited description",notes="Keep my notes",revision=1}); equivalent.EnsureSuccessStatusCode();
        var same=await equivalent.Content.ReadFromJsonAsync<JsonElement>(); Assert.True(same.GetProperty("sourceIsCurrent").GetBoolean()); Assert.Equal(1,same.GetProperty("sourceRevision").GetInt64());
        using var changed=await owner.PutAsJsonAsync($"/api/photographers/{bookmarkId}",new {name="Edited name",portfolioUrl="https://portfolio.example/new",summary="My edited description",notes="Keep my notes",revision=2}); changed.EnsureSuccessStatusCode();
        var next=await changed.Content.ReadFromJsonAsync<JsonElement>(); Assert.False(next.GetProperty("sourceIsCurrent").GetBoolean()); Assert.Equal(2,next.GetProperty("sourceRevision").GetInt64());
        factory.Clock.Advance(TimeSpan.FromHours(25)); using var fresh=await factory.CreateAuthenticatedClientAsync(subject);
        var persisted=await fresh.GetFromJsonAsync<JsonElement>($"/api/photographers/{bookmarkId}"); Assert.Equal("Casey",persisted.GetProperty("source").GetProperty("title").GetString()); Assert.Equal("Keep my notes",persisted.GetProperty("notes").GetString());
    }

    private static async Task<JsonElement> Start(HttpClient owner)
    {
        using var request=new HttpRequestMessage(HttpMethod.Post,"/api/photographer-drafts") {Content=JsonContent.Create(new {portfolioUrl="https://portfolio.example/"})}; request.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString());
        using var response=await owner.SendAsync(request); response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static async Task<HttpResponseMessage> Save(HttpClient owner,Guid id,object input,string? key=null)
    {
        using var request=new HttpRequestMessage(HttpMethod.Post,$"/api/photographer-drafts/{id}/save") {Content=JsonContent.Create(input)}; request.Headers.Add("Idempotency-Key",key??Guid.NewGuid().ToString()); return await owner.SendAsync(request);
    }
}
