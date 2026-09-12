using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Critiques;
using Loupe.Api.Tests.Photographs;
using Loupe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Loupe.Api.Tests.Search.SearchFixture;

namespace Loupe.Api.Tests.Search;

public sealed class SearchEligibilityTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Given_active_library_fields_when_searching_then_each_field_is_eligible_but_pending_and_My_Work_are_not()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var reference = await Save(owner, "references/links", new
        {
            title = "reference-title", sourceUrl = "https://reference-host.example/excluded-path?excluded-query#excluded-fragment",
            notes = "reference-notes", attribution = "reference-credit"
        }, "reference");
        using var description = await owner.PutAsJsonAsync($"/api/references/{reference}/description", new { revision = 1, text = "reference-description" });
        description.EnsureSuccessStatusCode();
        await SetTags(owner, reference, 2, "reference-tag");
        var photographer = await Save(owner, "photographers", new
        {
            name = "photographer-name", portfolioUrl = "https://photographer-host.example/excluded-path?excluded-query#excluded-fragment",
            summary = "photographer-summary", notes = "photographer-notes", tags = new[] { new { name = "photographer-tag" } }
        }, "photographer");
        await PendingReferenceSuggestions(factory, reference, "pending-description", "pending-reference-tag");
        await PendingPhotographerSuggestions(factory, photographer, "pending-summary", "pending-photographer-tag");
        var photograph = await PhotographFixture.UploadAsync(owner, "my-work-only");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            var id = photograph.GetProperty("id").GetGuid();
            var item = await store.Photographs.SingleAsync(item => item.Id == id);
            item.Notes = "my-work-notes";
            item.CritiqueJson = JsonSerializer.Serialize(CritiqueResultFixture.Valid());
            await store.SaveChangesAsync();
        }

        foreach (var field in new[] { "reference-title", "reference-host", "reference-notes", "reference-credit", "reference-description", "reference-tag" })
            Assert.Equal(new[] { reference }, Ids(await ReadSearch(owner, "query=" + field)));
        foreach (var field in new[] { "photographer-name", "photographer-host", "photographer-summary", "photographer-notes", "photographer-tag" })
            Assert.Equal(new[] { photographer }, Ids(await ReadSearch(owner, "query=" + field)));
        foreach (var excluded in new[] { "pending-description", "pending-reference-tag", "pending-summary", "pending-photographer-tag",
            "my-work-only", "my-work-notes", "synthetic", "excluded-path", "excluded-query", "excluded-fragment", "reference-title photographer-name" })
        {
            var result = await ReadSearch(owner, "query=" + Uri.EscapeDataString(excluded));
            Assert.Empty(Ids(result));
            Assert.Equal(0, result.GetProperty("totalCount").GetInt32());
        }
        var all = await ReadSearch(owner);
        Assert.Equal(new[] { reference, photographer }.Order(), Ids(all).Order());
        Assert.Equal(2, all.GetProperty("totalCount").GetInt32());
    }
}
