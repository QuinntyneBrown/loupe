using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Domain.Operations;
using Loupe.Domain.Photographers;
using Loupe.Domain.References;
using Loupe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.Search;

public static class SearchFixture
{
    public static async Task<Guid> Save(HttpClient owner, string route, object input, string property)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/" + route) { Content = JsonContent.Create(input) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await owner.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty(property).GetProperty("id").GetGuid();
    }

    public static async Task<JsonElement> ReadSearch(HttpClient owner, string query = "")
    {
        using var response = await owner.GetAsync("/api/search?" + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static Guid[] Ids(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray();

    public static async Task SetTags(HttpClient owner, Guid reference, long revision, params string[] names)
    {
        using var response = await owner.PutAsJsonAsync($"/api/references/{reference}/tags",
            new { revision, tags = names.Select(name => new { name }) });
        response.EnsureSuccessStatusCode();
    }

    public static async Task<Guid> PendingReferenceSuggestions(ApiFactory factory, Guid reference, string description, string tag)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var item = await store.References.SingleAsync(item => item.Id == reference);
        var operation = Guid.NewGuid();
        item.CurrentAnalysisOperationId = operation;
        item.SuggestionsJson = JsonSerializer.Serialize(new SavedReferenceSuggestions(operation, item.ImageRevision,
            factory.Clock.GetUtcNow(), ExecutionMode.Demo, "fixture", "fixture", description, "pending",
            [new ReferenceSuggestedTag(tag, "subject")]));
        await store.SaveChangesAsync();
        return operation;
    }

    public static async Task<Guid> PendingPhotographerSuggestions(ApiFactory factory, Guid photographer, string summary, string tag)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var item = await store.Photographers.SingleAsync(item => item.Id == photographer);
        var operation = Guid.NewGuid();
        item.CurrentSummaryOperationId = operation;
        item.SuggestionsJson = JsonSerializer.Serialize(new SavedPhotographerSuggestions(operation, item.SourceRevision,
            factory.Clock.GetUtcNow(), ExecutionMode.Demo, "fixture", "fixture",
            new CapturedPortfolioPage(item.PortfolioUrl, item.PortfolioUrl, factory.Clock.GetUtcNow(), item.Name, null, summary, []),
            summary, "pending", [new PhotographerSuggestedTag(tag, "subject")], null));
        await store.SaveChangesAsync();
        return operation;
    }
}
