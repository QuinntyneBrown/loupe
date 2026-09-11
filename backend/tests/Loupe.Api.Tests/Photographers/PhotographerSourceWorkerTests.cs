using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.References;
using Loupe.Api.Tests.Persistence;
using Loupe.Worker;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.Photographers;

public sealed class PhotographerSourceWorkerTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Standalone_worker_process_handles_a_portfolio_with_unavailable_dns()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot) { Settings = new Dictionary<string,string?> { ["Imports:Mode"] = "Live" } };
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var request = new HttpRequestMessage(HttpMethod.Post,"/api/photographer-drafts") { Content=JsonContent.Create(new {portfolioUrl="https://portfolio.invalid/"}) }; request.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString());
        using var response=await owner.SendAsync(request); response.EnsureSuccessStatusCode();
        var id=(await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await using var worker = new CleanupProcess(database.ConnectionString,database.MediaRoot,new Dictionary<string,string> { ["Imports__Mode"]="Live" });
        JsonElement draft=default;
        var deadline=DateTime.UtcNow.AddSeconds(15);
        do
        {
            await worker.EnsureRunningAsync(); draft=await owner.GetFromJsonAsync<JsonElement>($"/api/photographer-drafts/{id}");
            if (draft.GetProperty("import").GetProperty("status").GetString()=="Failed") break;
            await Task.Delay(100);
        } while (DateTime.UtcNow<deadline);
        Assert.Equal("Failed",draft.GetProperty("import").GetProperty("status").GetString());
        Assert.Equal("robots_unavailable",draft.GetProperty("failureCode").GetString());
    }

    [Fact]
    public async Task Page_reading_produces_editable_metadata_without_following_links_or_saving_a_bookmark()
    {
        using var transport = new ControlledSourceTransport((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath == "/robots.txt"
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<title>Casey Example</title><meta name='description' content='Portraits &amp; interiors.'><meta name='keywords' content='portrait, window light'><nav>Ignore navigation</nav><main><p>Casey works in available light.</p><a href='/gallery'>Gallery</a><script>Ignore scripts</script><p hidden>Private hidden text</p></main>", System.Text.Encoding.UTF8, "text/html") }));
        await using var factory = Factory(transport); using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = await Start(owner);
        var draft = await Process(factory, owner, id);
        Assert.Equal("Succeeded", draft.GetProperty("import").GetProperty("status").GetString());
        Assert.Equal("Casey Example", draft.GetProperty("name").GetString()); Assert.Equal("Portraits & interiors.", draft.GetProperty("description").GetString());
        Assert.Equal(new[] {"portrait","window light"}, draft.GetProperty("tags").EnumerateArray().Select(item=>item.GetString()));
        Assert.Equal("https://portfolio.example/", draft.GetProperty("source").GetProperty("fetchedUrl").GetString());
        Assert.Equal("Casey works in available light. Gallery", draft.GetProperty("source").GetProperty("mainText").GetString());
        Assert.DoesNotContain(transport.Requests, uri=>uri.AbsolutePath=="/gallery");
        var collection=await owner.GetFromJsonAsync<JsonElement>("/api/photographers"); Assert.Empty(collection.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Captured_metadata_and_main_text_are_bounded_by_unicode_scalars()
    {
        var title=string.Concat(Enumerable.Repeat("🌅",201)); var description=string.Concat(Enumerable.Repeat("🌅",4001)); var main=string.Concat(Enumerable.Repeat("🌅",20001));
        using var transport=new ControlledSourceTransport((request,_)=>Task.FromResult(request.RequestUri!.AbsolutePath=="/robots.txt"
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent($"<title>{title}</title><meta name='description' content='{description}'><main>{main}</main>",System.Text.Encoding.UTF8,"text/html")}));
        await using var factory=Factory(transport); using var owner=await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id=await Start(owner); var draft=await Process(factory,owner,id); Assert.Equal("Succeeded",draft.GetProperty("import").GetProperty("status").GetString());
        Assert.Equal(string.Concat(Enumerable.Repeat("🌅",200)),draft.GetProperty("name").GetString());
        Assert.Equal(string.Concat(Enumerable.Repeat("🌅",4000)),draft.GetProperty("description").GetString());
        Assert.Equal(string.Concat(Enumerable.Repeat("🌅",20000)),draft.GetProperty("source").GetProperty("mainText").GetString());
    }

    [Theory]
    [InlineData("robots", "robots_disallowed")]
    [InlineData("password", "source_access_denied")]
    [InlineData("paywall", "source_access_denied")]
    public async Task Restricted_pages_leave_a_manual_fallback(string restriction,string expectedCode)
    {
        using var transport=new ControlledSourceTransport((request,_)=>Task.FromResult(request.RequestUri!.AbsolutePath=="/robots.txt"
            ? new HttpResponseMessage(restriction=="robots"?HttpStatusCode.OK:HttpStatusCode.NotFound) {Content=new StringContent("User-agent: Loupe\nDisallow: /\n")}
            : new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent(restriction=="password"?"<input type='password'><main>Protected biography</main>":"<script type='application/ld+json'>{\"isAccessibleForFree\":false}</script><main>Paid biography</main>",System.Text.Encoding.UTF8,"text/html")}));
        await using var factory=Factory(transport); using var owner=await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id=await Start(owner); var draft=await Process(factory,owner,id);
        Assert.Equal("Failed",draft.GetProperty("import").GetProperty("status").GetString()); Assert.Equal(expectedCode,draft.GetProperty("failureCode").GetString());
        Assert.Equal(JsonValueKind.Null,draft.GetProperty("description").ValueKind);
        if (restriction=="robots") Assert.All(transport.Requests,uri=>Assert.Equal("/robots.txt",uri.AbsolutePath));
    }

    private ApiFactory Factory(HttpMessageHandler transport)=>new(database.ConnectionString,database.MediaRoot) {SourceTransport=transport,Settings=new Dictionary<string,string?> {["Imports:Mode"]="Live"}};
    private static async Task<Guid> Start(HttpClient owner)
    {
        using var request=new HttpRequestMessage(HttpMethod.Post,"/api/photographer-drafts") {Content=JsonContent.Create(new {portfolioUrl="https://portfolio.example/"})}; request.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString());
        using var response=await owner.SendAsync(request); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    private static async Task<JsonElement> Process(ApiFactory factory,HttpClient owner,Guid id)
    {
        using var worker=ActivatorUtilities.CreateInstance<ReferenceImportWorker>(factory.Services); await worker.StartAsync(CancellationToken.None);
        try
        {
            JsonElement result=default;
            for (var attempt=0;attempt<40;attempt++)
            {
                result=await owner.GetFromJsonAsync<JsonElement>($"/api/photographer-drafts/{id}");
                if (result.GetProperty("import").GetProperty("status").GetString() is "Succeeded" or "Failed") return result;
                await Task.Delay(50);
            }
            return result;
        }
        finally {await worker.StopAsync(CancellationToken.None);}
    }
}
