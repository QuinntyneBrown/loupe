// Given a private collection, when pages are traversed, then every photograph appears once in creation/id order.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class ListPhotographTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_003_1_Default_pages_preserve_order_and_isolate_owners()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        var subject = Guid.NewGuid().ToString();
        using var client = await factory.CreateAuthenticatedClientAsync(subject);
        using var other = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var expected = new List<JsonElement>();
        for (var index = 0; index < 25; index++)
        {
            expected.Add(await PhotographFixture.UploadAsync(client, $"Study {index}"));
            if (index == 11) factory.Clock.Advance(TimeSpan.FromMinutes(1));
        }
        await PhotographFixture.UploadAsync(other, "Not yours");
        using var firstResponse = await client.GetAsync("/api/photographs");
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var first = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(24, first.GetProperty("items").GetArrayLength());
        var cursor = first.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(cursor));
        await using var second = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var later = await second.CreateAuthenticatedClientAsync(subject);
        var next = await later.GetFromJsonAsync<JsonElement>("/api/photographs?cursor=" + Uri.EscapeDataString(cursor!));
        Assert.Single(next.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, next.GetProperty("nextCursor").ValueKind);
        var actual = first.GetProperty("items").EnumerateArray().Concat(next.GetProperty("items").EnumerateArray()).ToArray();
        var ordered = expected.OrderByDescending(item => item.GetProperty("createdAt").GetDateTimeOffset())
            .ThenBy(item => item.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(ordered.Select(item => item.GetProperty("id").GetGuid()), actual.Select(item => item.GetProperty("id").GetGuid()));
        Assert.Equal(ordered.Select(item => item.GetProperty("title").GetString()), actual.Select(item => item.GetProperty("title").GetString()));
        Assert.All(actual, item => Assert.StartsWith("/api/photographs/", item.GetProperty("previewUrl").GetString()));
        var foreign = await other.GetFromJsonAsync<JsonElement>("/api/photographs");
        Assert.Single(foreign.GetProperty("items").EnumerateArray());
        Assert.Equal("Not yours", foreign.GetProperty("items")[0].GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("?pageSize=1")]
    [InlineData("?pageSize=100")]
    public async Task L2_003_4_Empty_library_is_a_successful_empty_page(string query)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var response = await client.GetAsync("/api/photographs" + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(page.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, page.GetProperty("nextCursor").ValueKind);
    }

    [Theory]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("cursor=invalid", "cursor")]
    [InlineData("cursor=e30%3D", "cursor")]
    public async Task L2_003_Invalid_pagination_returns_field_errors(string query, string field)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync();
        using var response = await client.GetAsync("/api/photographs?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }
}
