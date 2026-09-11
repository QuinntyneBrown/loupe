using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.References;

// Given active tags on private references, filters combine with AND across the
// entire result set and remain bound to a board and cursor (L2-012, L2-025).
public sealed class ReferenceFilterTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Tag_filters_are_private_counted_and_combine_with_boards_before_pagination()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var created = await owner.PostAsJsonAsync("/api/boards", new { name = "Studies" });
        var board = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        for (var index = 0; index < 4; index++)
        {
            using var upload = await ReferenceFixture.SubmitAsync(owner);
            var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            using var tags = await owner.PutAsJsonAsync($"/api/references/{id}/tags", new { revision = 1, tags = (index == 3 ? new[] { "soft light" } : new[] { "soft light", "portrait" }).Select(name => new { name }) });
            tags.EnsureSuccessStatusCode();
            if (index != 0)
            {
                using var membership = await owner.PutAsJsonAsync($"/api/references/{id}/boards", new { revision = 2, boardIds = new[] { board } });
                membership.EnsureSuccessStatusCode();
            }
        }
        var url = $"/api/references?boardId={board}&tags=SOFT%20LIGHT&tags=portrait&pageSize=1";
        var first = await owner.GetFromJsonAsync<JsonElement>(url);
        Assert.Single(first.GetProperty("items").EnumerateArray());
        Assert.Equal(2, first.GetProperty("totalCount").GetInt32());
        Assert.Equal(4, first.GetProperty("libraryCount").GetInt32());
        var cursor = Uri.EscapeDataString(first.GetProperty("nextCursor").GetString()!);
        var second = await owner.GetFromJsonAsync<JsonElement>(url + "&cursor=" + cursor);
        Assert.Single(second.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, second.GetProperty("nextCursor").ValueKind);
        using var reused = await owner.GetAsync($"/api/references?boardId={board}&tags=portrait&pageSize=1&cursor={cursor}");
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
        var absent = await owner.GetFromJsonAsync<JsonElement>("/api/references?tags=unknown");
        Assert.Empty(absent.GetProperty("items").EnumerateArray());
        using var facetResponse = await owner.GetAsync($"/api/references/tags?boardId={board}");
        Assert.True(facetResponse.IsSuccessStatusCode, factory.Failure.Exception?.ToString());
        var facets = await facetResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, facets.GetArrayLength());
        Assert.Contains(facets.EnumerateArray(), tag => tag.GetProperty("name").GetString() == "soft light" && tag.GetProperty("referenceCount").GetInt32() == 3);
        var privateFacets = await stranger.GetFromJsonAsync<JsonElement>("/api/references/tags");
        Assert.Empty(privateFacets.EnumerateArray());
        using var privateBoard = await stranger.GetAsync($"/api/references/tags?boardId={board}");
        Assert.Equal(HttpStatusCode.NotFound, privateBoard.StatusCode);
    }

    [Fact]
    public async Task Too_many_filters_and_null_tag_entries_are_validation_errors()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var filters = await owner.GetAsync("/api/references?" + string.Join('&', Enumerable.Range(0, 11).Select(i => $"tags=tag{i}")));
        Assert.Equal(HttpStatusCode.BadRequest, filters.StatusCode);
        using var upload = await ReferenceFixture.SubmitAsync(owner);
        var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var invalid = await owner.PutAsJsonAsync($"/api/references/{id}/tags", new { revision = 1, tags = new object?[] { null } });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
}
