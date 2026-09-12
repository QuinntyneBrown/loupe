using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using static Loupe.Api.Tests.Search.SearchFixture;

namespace Loupe.Api.Tests.Search;

public sealed class SearchTagFacetTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData("straße", "STRAßE", " StrAße ", "strasse", "STRASSE", "StrAsSe")]
    [InlineData("ı", "ı", " ı ", "i", "I", " I ")]
    [InlineData("ﬀ", "ﬀ", " ﬀ ", "ff", "FF", "Ff")]
    [InlineData("café", "CAFÉ", " Cafe\u0301 ", "portrait", "PORTRAIT", "PORTRAIT")]
    public async Task Given_distinct_persisted_tag_identities_when_resolving_URL_aliases_then_keep_facets_and_AND_filters_private_and_independent(
        string left, string leftKey, string leftAlias, string right, string rightKey, string rightAlias)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var leftOnly = await Save(owner, "references/links", new { sourceUrl = "https://left.example/" }, "reference");
        await SetTags(owner, leftOnly, 1, left);
        var rightOnly = await Save(owner, "photographers", new
        {
            name = "Right", portfolioUrl = "https://right.example/", tags = new[] { new { name = right } }
        }, "photographer");
        var bothReference = await Save(owner, "references/links", new { sourceUrl = "https://both.example/" }, "reference");
        await SetTags(owner, bothReference, 1, left, right);
        var bothPhotographer = await Save(owner, "photographers", new
        {
            name = "Both", portfolioUrl = "https://both-photographer.example/", tags = new[] { new { name = left }, new { name = right } }
        }, "photographer");
        await PendingReferenceSuggestions(factory, leftOnly, "Pending", "pending-reference");
        await PendingPhotographerSuggestions(factory, rightOnly, "Pending", "pending-photographer");
        var privateReference = await Save(stranger, "references/links", new { sourceUrl = "https://private.example/" }, "reference");
        await SetTags(stranger, privateReference, 1, left, right, "private-reference");
        await Save(stranger, "photographers", new
        {
            name = "Private", portfolioUrl = "https://private-photographer.example/",
            tags = new[] { new { name = left }, new { name = right }, new { name = "private-photographer" } }
        }, "photographer");

        var facets = await Facets(owner, leftAlias, rightAlias, left, "unknown", "private-reference", "pending-photographer");
        Assert.Equal(2, facets.Length);
        var leftFacet = Assert.Single(facets, facet => facet.GetProperty("normalizedName").GetString() == leftKey);
        var rightFacet = Assert.Single(facets, facet => facet.GetProperty("normalizedName").GetString() == rightKey);
        Assert.Equal(left, leftFacet.GetProperty("name").GetString());
        Assert.Equal(right, rightFacet.GetProperty("name").GetString());
        Assert.All(facets, facet => Assert.Equal(3, facet.GetProperty("count").GetInt32()));
        Assert.Equal(new[] { leftAlias, left }, leftFacet.GetProperty("selectedNames").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(new[] { rightAlias }, rightFacet.GetProperty("selectedNames").EnumerateArray().Select(value => value.GetString()));
        Assert.All(await Facets(owner), facet => Assert.Empty(facet.GetProperty("selectedNames").EnumerateArray()));

        var both = await ReadSearch(owner, $"tags={Uri.EscapeDataString(leftAlias)}&tags={Uri.EscapeDataString(rightAlias)}");
        Assert.Equal(new[] { bothReference, bothPhotographer }.Order(), Ids(both).Order());
        Assert.Equal(2, both.GetProperty("totalCount").GetInt32());
        Assert.Equal(new[] { leftOnly, bothReference, bothPhotographer }.Order(),
            Ids(await ReadSearch(owner, "tags=" + Uri.EscapeDataString(leftAlias))).Order());
        Assert.Equal(new[] { rightOnly, bothReference, bothPhotographer }.Order(),
            Ids(await ReadSearch(owner, "tags=" + Uri.EscapeDataString(rightAlias))).Order());
    }

    [Fact]
    public async Task Given_mixed_active_tags_when_reading_facets_then_return_complete_private_normalized_choices()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var reference = await Save(owner, "references/links", new { title = "Oldest", sourceUrl = "https://reference.example/" }, "reference");
        await SetTags(owner, reference, 1, " Cafe\u0301 ", "reference-only");
        await PendingReferenceSuggestions(factory, reference, "Pending description", "pending-only");
        var photographer = await Save(owner, "photographers", new
        {
            name = "Bookmark", portfolioUrl = "https://photographer.example/",
            tags = new[] { new { name = "CAFÉ" }, new { name = "photographer-only" } }
        }, "photographer");
        await PendingPhotographerSuggestions(factory, photographer, "Pending summary", "pending-photographer-only");
        await Save(stranger, "photographers", new
        {
            name = "Private", portfolioUrl = "https://private.example/",
            tags = new[] { new { name = "CAFÉ" }, new { name = "private-only" } }
        }, "photographer");
        var privateReference = await Save(stranger, "references/links", new { sourceUrl = "https://private-reference.example/" }, "reference");
        await SetTags(stranger, privateReference, 1, "CAFÉ", "private-reference-only");
        factory.Clock.Advance(TimeSpan.FromSeconds(1));
        for (var index = 0; index < 24; index++)
            await Save(owner, "references/links", new { sourceUrl = $"https://newer.example/{index}" }, "reference");
        Assert.Equal(24, (await ReadSearch(owner)).GetProperty("items").GetArrayLength());

        var facets = await Facets(owner);
        Assert.Equal(new[] { 2, 1, 1 }, facets.Select(facet => facet.GetProperty("count").GetInt32()));
        Assert.Equal(new[] { "CAFÉ", "photographer-only", "reference-only" }, facets.Select(facet => facet.GetProperty("name").GetString()));
        Assert.Equal(JsonSerializer.Serialize(facets), JsonSerializer.Serialize(await Facets(owner)));

        await SetTags(owner, reference, 2, "renamed");
        Assert.Equal(new[] { "CAFÉ", "photographer-only", "renamed" }, (await Facets(owner)).Select(facet => facet.GetProperty("name").GetString()));
        using var edited = await owner.PutAsJsonAsync($"/api/photographers/{photographer}", new
        {
            revision = 1, name = "Bookmark", portfolioUrl = "https://photographer.example/", tags = Array.Empty<object>()
        });
        edited.EnsureSuccessStatusCode();
        Assert.Equal("renamed", Assert.Single(await Facets(owner)).GetProperty("name").GetString());
        using var deleted = await owner.DeleteAsync($"/api/references/{reference}?revision=3");
        deleted.EnsureSuccessStatusCode();
        Assert.Empty(await Facets(owner));
    }

    [Fact]
    public async Task Given_no_active_tags_when_reading_facets_then_return_empty_only_for_an_authenticated_owner()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var anonymous = factory.CreateClient();
        using var denied = await anonymous.GetAsync("/api/search/tags");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        Assert.Empty(await Facets(owner));
    }

    [Theory]
    [InlineData("selectedTags")]
    [InlineData("selectedTags=")]
    [InlineData("selectedTags=%20%09")]
    [InlineData("selectedTags=valid&selectedTags=")]
    [InlineData("selectedTags[0]=")]
    [InlineData("selectedTags=bad%00tag")]
    [InlineData("selectedTags=abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz")]
    public async Task Given_invalid_facet_aliases_when_reading_choices_then_return_a_selectedTags_field_error(string query)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var response = await owner.GetAsync("/api/search/tags?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotEmpty((await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("errors").GetProperty("selectedTags").EnumerateArray());
    }

    [Fact]
    public async Task Given_facet_alias_limits_when_reading_choices_then_accept_ten_Unicode_names_and_reject_an_eleventh()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var reference = await Save(owner, "references/links", new { sourceUrl = "https://boundary.example/" }, "reference");
        var names = Enumerable.Range(1, 9).Select(index => "tag " + index)
            .Append(string.Concat(Enumerable.Repeat("😀", 50))).ToArray();
        await SetTags(owner, reference, 1, names);
        var facets = await Facets(owner, names);
        Assert.Equal(10, facets.Length);
        Assert.All(facets, facet => Assert.Equal(facet.GetProperty("name").GetString(),
            Assert.Single(facet.GetProperty("selectedNames").EnumerateArray()).GetString()));
        using var excessive = await owner.GetAsync("/api/search/tags?" +
            string.Join('&', names.Append("eleventh").Select(name => "selectedTags=" + Uri.EscapeDataString(name))));
        Assert.Equal(HttpStatusCode.BadRequest, excessive.StatusCode);
        Assert.NotEmpty((await excessive.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("errors").GetProperty("selectedTags").EnumerateArray());
        using var oversized = await owner.GetAsync("/api/search/tags?selectedTags=" +
            Uri.EscapeDataString(names[^1] + "😀"));
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
    }

    private static async Task<JsonElement[]> Facets(HttpClient owner, params string[] selectedTags)
    {
        using var response = await owner.GetAsync("/api/search/tags?" +
            string.Join('&', selectedTags.Select(tag => "selectedTags=" + Uri.EscapeDataString(tag))));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToArray();
    }
}
