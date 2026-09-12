// Given an owned inspiration library, when it is traversed, then stable pages
// contain only references and never disclose another owner's records.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Photographs;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Loupe.Api.Tests.References;

public sealed class ListReferencesTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_012_Cards_include_owned_source_and_attribution_without_detail_reads()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var upload = await ReferenceFixture.SubmitAsync(owner, new Dictionary<string, string>
        {
            ["attribution"] = "Mara Lindqvist",
            ["sourceUrl"] = "https://example.com/photograph"
        });
        upload.EnsureSuccessStatusCode();
        var page = await owner.GetFromJsonAsync<JsonElement>("/api/references");
        var item = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal("Mara Lindqvist", item.GetProperty("attribution").GetString());
        Assert.Equal("https://example.com/photograph", item.GetProperty("sourceUrl").GetString());
    }

    [Fact]
    public async Task L2_009_1_012_1_References_page_in_stable_order_separately_from_My_Work_and_other_owners()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var saved = new List<JsonElement>();
        for (var index = 0; index < 25; index++)
        {
            using var upload = await ReferenceFixture.SubmitAsync(owner, new Dictionary<string, string> { ["title"] = $"Reference {index}" });
            upload.EnsureSuccessStatusCode();
            saved.Add(await upload.Content.ReadFromJsonAsync<JsonElement>());
            if (index % 5 == 0) factory.Clock.Advance(TimeSpan.FromSeconds(1));
        }
        using var foreign = await ReferenceFixture.SubmitAsync(stranger);
        foreign.EnsureSuccessStatusCode();
        await PhotographFixture.UploadAsync(owner);
        using var response = await owner.GetAsync("/api/references");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var first = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(24, first.GetProperty("items").GetArrayLength());
        var cursor = first.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrEmpty(cursor));
        var second = await owner.GetFromJsonAsync<JsonElement>("/api/references?cursor=" + Uri.EscapeDataString(cursor!));
        Assert.Single(second.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, second.GetProperty("nextCursor").ValueKind);
        var items = first.GetProperty("items").EnumerateArray().Concat(second.GetProperty("items").EnumerateArray()).ToArray();
        var expected = saved.OrderByDescending(item => item.GetProperty("createdAt").GetDateTimeOffset()).ThenBy(item => item.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(expected.Select(item => item.GetProperty("id").GetGuid()), items.Select(item => item.GetProperty("id").GetGuid()));
        for (var index = 0; index < items.Length; index++)
        {
            Assert.Equal(expected[index].GetProperty("title").GetString(), items[index].GetProperty("title").GetString());
            Assert.Equal(expected[index].GetProperty("previewUrl").GetString(), items[index].GetProperty("previewUrl").GetString());
        }
        var photographs = await owner.GetFromJsonAsync<JsonElement>("/api/photographs");
        Assert.Single(photographs.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task L2_012_5_An_empty_owned_library_is_a_successful_empty_page()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var response = await owner.GetAsync("/api/references");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(page.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, page.GetProperty("nextCursor").ValueKind);
    }

    [Theory]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("cursor=invalid", "cursor")]
    public async Task L2_012_5_Invalid_page_parameters_return_field_errors(string query, string field)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var response = await owner.GetAsync("/api/references?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task L2_038_An_anonymous_visitor_cannot_list_references()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, BaseAddress = new Uri("https://localhost") });
        using var response = await client.GetAsync("/api/references");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
