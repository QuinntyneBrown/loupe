using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Photographs;
using Loupe.Api.Tests.References;
using Xunit;

namespace Loupe.Api.Tests.Locations;

// Acceptance Test
// Traces to: L2-057
// Description: Browse an owned location library in stable pages that show each
// location once with its cover, name, locality, image count, and report status,
// and never a photograph, a reference, or another owner's location.
public sealed class BrowseLocationsTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    // L2-057, criterion 1: 25 locations page in the shared order, once each, without the photograph or the reference.
    [Fact]
    public async Task L2_057_1_Locations_page_in_stable_order_separately_from_other_areas_and_owners()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var saved = new List<JsonElement>();
        for (var index = 0; index < 25; index++)
        {
            saved.Add(await CreateAsync(owner, new { name = $"Location {index}", locality = index % 2 == 0 ? "Brentford" : null }));
            if (index % 5 == 0) factory.Clock.Advance(TimeSpan.FromSeconds(1));
        }
        await CreateAsync(stranger, new { name = "Foreign" });
        using var reference = await ReferenceFixture.SubmitAsync(owner);
        reference.EnsureSuccessStatusCode();
        await PhotographFixture.UploadAsync(owner);

        var first = await owner.GetFromJsonAsync<JsonElement>("/api/locations?pageSize=10");
        Assert.Equal(10, first.GetProperty("items").GetArrayLength());
        Assert.Equal(25, first.GetProperty("totalCount").GetInt32());
        var second = await owner.GetFromJsonAsync<JsonElement>("/api/locations?pageSize=10&cursor=" + Uri.EscapeDataString(first.GetProperty("nextCursor").GetString()!));
        Assert.Equal(10, second.GetProperty("items").GetArrayLength());
        var third = await owner.GetFromJsonAsync<JsonElement>("/api/locations?pageSize=10&cursor=" + Uri.EscapeDataString(second.GetProperty("nextCursor").GetString()!));
        Assert.Equal(5, third.GetProperty("items").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, third.GetProperty("nextCursor").ValueKind);

        var items = first.GetProperty("items").EnumerateArray().Concat(second.GetProperty("items").EnumerateArray()).Concat(third.GetProperty("items").EnumerateArray()).ToArray();
        var expected = saved.OrderByDescending(item => item.GetProperty("createdAt").GetDateTimeOffset()).ThenBy(item => item.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(expected.Select(item => item.GetProperty("id").GetGuid()), items.Select(item => item.GetProperty("id").GetGuid()));
        for (var index = 0; index < items.Length; index++)
        {
            Assert.Equal(expected[index].GetProperty("name").GetString(), items[index].GetProperty("name").GetString());
            Assert.Equal(expected[index].GetProperty("locality").GetRawText(), items[index].GetProperty("locality").GetRawText());
            Assert.Equal(JsonValueKind.Null, items[index].GetProperty("coverPreviewUrl").ValueKind);
            Assert.Equal(0, items[index].GetProperty("imageCount").GetInt32());
            Assert.Equal("None", items[index].GetProperty("reportStatus").GetString());
        }
        Assert.Equal(24, (await owner.GetFromJsonAsync<JsonElement>("/api/locations")).GetProperty("items").GetArrayLength());
        Assert.Single((await owner.GetFromJsonAsync<JsonElement>("/api/photographs")).GetProperty("items").EnumerateArray());
        Assert.Single((await owner.GetFromJsonAsync<JsonElement>("/api/references")).GetProperty("items").EnumerateArray());
        var foreign = await stranger.GetFromJsonAsync<JsonElement>("/api/locations");
        Assert.Equal("Foreign", Assert.Single(foreign.GetProperty("items").EnumerateArray()).GetProperty("name").GetString());
    }

    // L2-057, criterion 4: an empty library is a successful empty page; bad paging parameters name their field.
    [Fact]
    public async Task L2_057_4_An_empty_owned_library_is_a_successful_empty_page()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var response = await owner.GetAsync("/api/locations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(page.GetProperty("items").EnumerateArray());
        Assert.Equal(0, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, page.GetProperty("nextCursor").ValueKind);
    }

    [Theory]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("cursor=invalid", "cursor")]
    public async Task L2_057_4_Invalid_page_parameters_return_field_errors(string query, string field)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var response = await owner.GetAsync("/api/locations?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task L2_057_4_A_cursor_from_another_view_is_rejected()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        for (var index = 0; index < 3; index++) await CreateAsync(owner, new { name = $"Location {index}" });
        var page = await owner.GetFromJsonAsync<JsonElement>("/api/locations?pageSize=2");
        var cursor = Uri.EscapeDataString(page.GetProperty("nextCursor").GetString()!);
        using var otherSize = await owner.GetAsync("/api/locations?pageSize=3&cursor=" + cursor);
        Assert.Equal(HttpStatusCode.BadRequest, otherSize.StatusCode);
        Assert.True((await otherSize.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("cursor", out _));
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var otherOwner = await stranger.GetAsync("/api/locations?pageSize=2&cursor=" + cursor);
        Assert.Equal(HttpStatusCode.BadRequest, otherOwner.StatusCode);
    }

    private static async Task<JsonElement> CreateAsync(HttpClient owner, object input)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/locations") { Content = JsonContent.Create(input) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await owner.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
