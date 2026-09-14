using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Locations;
using Loupe.Api.Tests.Search;
using Loupe.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Loupe.Api.Tests.ShootPlanning;

// Acceptance Test
// Traces to: L2-061, L2-062, L2-026
// Description: Meaning mode ranks the owner's current location vectors by cosine
// similarity against the embedded query — relevant locations surface without the
// query's words in their tags, filters apply before pagination, scores under 0.20
// drop out, ties order by identifier, a cursor minted under another model is
// refused, a blank query is refused before any embedding, an unavailable embedding
// service reports search unavailable while keyword still works, an edited location
// waits for its re-index, and a deleted one never surfaces.
public sealed class FindLocationsMeaningTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private const string Query = "Golden hour engagement session with leading lines for two people";

    // L2-061, criterion 1: the canonical query returns the relevant location first without any query word in its tags, with the card fields of every result.
    [Fact]
    public async Task L2_061_1_The_canonical_query_ranks_the_relevant_location_first_without_its_words_in_tags()
    {
        using var transport = Embeddings(("Kew foreshore", Axis(1)), ("Peckham roof", Blend((1, 0.6f), (2, 0.8f))), ("Window studio", Axis(3)));
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var kew = await CreateAsync(owner, new { name = "Kew foreshore", locality = "Brentford", tags = new[] { new { name = "river" } } });
        var peckham = await CreateAsync(owner, new { name = "Peckham roof", locality = "Southwark" });
        var studio = await CreateAsync(owner, new { name = "Window studio" });
        await IndexAsync(factory, owner, kew, peckham, studio);

        var page = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/search?mode=meaning&query={Uri.EscapeDataString(Query)}");
        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
        var items = page.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal([kew, peckham], items.Select(item => item.GetProperty("id").GetGuid()).ToArray());
        Assert.Equal("Kew foreshore", items[0].GetProperty("name").GetString());
        Assert.Equal("Brentford", items[0].GetProperty("locality").GetString());
        Assert.Equal(0, items[0].GetProperty("imageCount").GetInt32());
        Assert.Equal("None", items[0].GetProperty("reportStatus").GetString());
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("groupSize").ValueKind);
        Assert.Empty(items[0].GetProperty("recommendedPeriods").EnumerateArray());
        Assert.DoesNotContain(transport.Requests, request => request.Body.Contains(Query) && request.Body.Contains("Kew foreshore"));
        Assert.Single(transport.Requests, request => request.Body.Contains(Query));
    }

    // L2-061, criteria 3 and 4 shape for meaning; L2-026, criterion 3: filters narrow the ranked candidates before paging, and a page never underfills while eligible candidates remain.
    [Fact]
    public async Task L2_026_3_Filters_apply_before_pagination_and_pages_do_not_underfill()
    {
        using var transport = Embeddings(("Alpha", Axis(1)), ("Bravo", Axis(1)), ("Charlie", Axis(1)), ("Delta", Axis(1)), ("Echo", Blend((1, 0.1f), (2, 0.99f))));
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var alpha = await CreateAsync(owner, new { name = "Alpha", setting = "Indoor" });
        var bravo = await CreateAsync(owner, new { name = "Bravo", setting = "Outdoor" });
        var charlie = await CreateAsync(owner, new { name = "Charlie", setting = "Indoor" });
        var delta = await CreateAsync(owner, new { name = "Delta", setting = "Outdoor" });
        var echo = await CreateAsync(owner, new { name = "Echo", setting = "Outdoor" });
        await IndexAsync(factory, owner, alpha, bravo, charlie, delta, echo);
        var outdoor = new[] { bravo, delta }.Order().ToArray();

        var first = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/search?mode=meaning&query={Uri.EscapeDataString(Query)}&setting=Outdoor&pageSize=1");
        Assert.Equal(2, first.GetProperty("totalCount").GetInt32());
        Assert.Equal([outdoor[0]], first.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray());
        var cursor = Uri.EscapeDataString(first.GetProperty("nextCursor").GetString()!);
        var second = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/search?mode=meaning&query={Uri.EscapeDataString(Query)}&setting=Outdoor&pageSize=1&cursor={cursor}");
        Assert.Equal([outdoor[1]], second.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray());
        Assert.Equal(JsonValueKind.Null, second.GetProperty("nextCursor").ValueKind);
        var all = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/search?mode=meaning&query={Uri.EscapeDataString(Query)}");
        Assert.Equal(4, all.GetProperty("totalCount").GetInt32());
        Assert.DoesNotContain(echo, all.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
    }

    // L2-061, criterion 8; L2-026, criterion 6: equal scores order by identifier across pages, and a cursor minted under another embedding model is refused with refresh_required.
    [Fact]
    public async Task L2_061_8_Ties_order_by_identifier_and_a_cursor_from_another_model_is_refused()
    {
        using var transport = Embeddings(("Alpha", Axis(1)), ("Bravo", Axis(1)), ("Charlie", Axis(1)));
        await using var factory = Factory(transport);
        var subject = Guid.NewGuid().ToString();
        using var owner = await factory.CreateAuthenticatedClientAsync(subject);
        var ids = new List<Guid>();
        foreach (var name in new[] { "Alpha", "Bravo", "Charlie" }) ids.Add(await CreateAsync(owner, new { name }));
        await IndexAsync(factory, owner, ids.ToArray());
        var expected = ids.Order().ToArray();

        var first = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/search?mode=meaning&query={Uri.EscapeDataString(Query)}&pageSize=2");
        Assert.Equal(expected[..2], first.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray());
        var cursor = Uri.EscapeDataString(first.GetProperty("nextCursor").GetString()!);
        var second = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/search?mode=meaning&query={Uri.EscapeDataString(Query)}&pageSize=2&cursor={cursor}");
        Assert.Equal([expected[2]], second.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray());
        using var wrongScope = await owner.GetAsync($"/api/locations/search?mode=meaning&query={Uri.EscapeDataString(Query)}&pageSize=3&cursor={cursor}");
        Assert.Equal(HttpStatusCode.BadRequest, wrongScope.StatusCode);

        using var otherTransport = Embeddings(("Alpha", Axis(1)));
        await using var other = new ApiFactory(database.ConnectionString, database.MediaRoot)
        {
            EmbeddingTransport = otherTransport,
            Settings = new Dictionary<string, string?> { ["Embeddings:Endpoint"] = "http://ollama.test:11434", ["Embeddings:Model"] = "other-model" }
        };
        using var sameOwner = await other.CreateAuthenticatedClientAsync(subject);
        using var refused = await sameOwner.GetAsync($"/api/locations/search?mode=meaning&query={Uri.EscapeDataString(Query)}&pageSize=2&cursor={cursor}");
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("refresh_required", (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        // Vectors built under the previous model are never compared with the new model's query.
        var fresh = await sameOwner.GetFromJsonAsync<JsonElement>($"/api/locations/search?mode=meaning&query={Uri.EscapeDataString(Query)}");
        Assert.Equal(0, fresh.GetProperty("totalCount").GetInt32());
    }

    // L2-061, criterion 6: a blank Meaning query is refused before any embedding is generated; Keyword with filters and no query still browses.
    [Fact]
    public async Task L2_061_6_A_blank_meaning_query_is_refused_without_an_embedding_call()
    {
        using var transport = Embeddings();
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = await CreateAsync(owner, new { name = "Kew foreshore", setting = "Outdoor" });
        foreach (var query in new[] { "", "%20%20" })
        {
            using var refused = await owner.GetAsync($"/api/locations/search?mode=meaning&query={query}&setting=Outdoor");
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            var body = await refused.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("invalid_request", body.GetProperty("code").GetString());
            Assert.True(body.GetProperty("errors").TryGetProperty("query", out _));
        }
        Assert.Empty(transport.Requests);
        Assert.Equal([id], (await owner.GetFromJsonAsync<JsonElement>("/api/locations/search?setting=Outdoor")).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray());
    }

    // L2-061, criterion 7; L2-026, criterion 4: an unconfigured or failing embedding service reports search unavailable, never keyword matches, while Keyword keeps working.
    [Fact]
    public async Task L2_061_7_An_unavailable_embedding_service_reports_search_unavailable_while_keyword_works()
    {
        using var failing = new ControlledEmbeddingTransport((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        await using var broken = Factory(failing);
        using var owner = await broken.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = await CreateAsync(owner, new { name = "Kew foreshore", notes = "Leading lines along the foreshore." });
        using var unavailable = await owner.GetAsync($"/api/locations/search?mode=meaning&query={Uri.EscapeDataString(Query)}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Equal("search_unavailable", (await unavailable.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal([id], (await owner.GetFromJsonAsync<JsonElement>("/api/locations/search?query=leading+lines")).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray());

        await using var unconfigured = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var another = await unconfigured.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        await CreateAsync(another, new { name = "Kew foreshore" });
        using var notConfigured = await another.GetAsync($"/api/locations/search?mode=meaning&query={Uri.EscapeDataString(Query)}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, notConfigured.StatusCode);
        Assert.Equal("search_unavailable", (await notConfigured.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Single((await another.GetFromJsonAsync<JsonElement>("/api/locations/search?query=kew")).GetProperty("items").EnumerateArray());
    }

    // L2-062, criteria 5 and 7: an edited location leaves Meaning results until its re-index lands while keyword finds it at once, and a deleted location never surfaces.
    [Fact]
    public async Task L2_062_5_7_An_edit_excludes_the_stale_vector_until_reindexed_and_a_deleted_location_never_surfaces()
    {
        using var transport = Embeddings(("Kew foreshore", Axis(1)), ("Peckham roof", Axis(1)));
        await using var factory = Factory(transport);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var kew = await CreateAsync(owner, new { name = "Kew foreshore" });
        var peckham = await CreateAsync(owner, new { name = "Peckham roof" });
        await IndexAsync(factory, owner, kew, peckham);
        Assert.Equal(2, (await Meaning(owner)).GetProperty("totalCount").GetInt32());

        var current = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{kew}");
        using var edit = await owner.PutAsJsonAsync($"/api/locations/{kew}/notes", new { revision = current.GetProperty("revision").GetInt64(), text = "Heron at dawn." });
        edit.EnsureSuccessStatusCode();
        var stale = await Meaning(owner);
        Assert.Equal([peckham], stale.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray());
        Assert.Equal(1, stale.GetProperty("totalCount").GetInt32());
        Assert.Equal([kew], (await owner.GetFromJsonAsync<JsonElement>("/api/locations/search?query=heron")).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray());
        await IndexAsync(factory, owner, kew);
        Assert.Equal(2, (await Meaning(owner)).GetProperty("totalCount").GetInt32());

        var latest = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{peckham}");
        using var deletion = await owner.DeleteAsync($"/api/locations/{peckham}?revision={latest.GetProperty("revision").GetInt64()}");
        deletion.EnsureSuccessStatusCode();
        var afterDeletion = await Meaning(owner);
        Assert.Equal([kew], afterDeletion.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray());
        Assert.Equal(1, afterDeletion.GetProperty("totalCount").GetInt32());
    }

    private static Task<JsonElement> Meaning(HttpClient owner) => owner.GetFromJsonAsync<JsonElement>($"/api/locations/search?mode=meaning&query={Uri.EscapeDataString(Query)}");

    private static async Task<Guid> CreateAsync(HttpClient owner, object input) => (await LocationFixture.CreateAsync(owner, input)).GetProperty("id").GetGuid();

    /// <summary>Runs the index worker until every named location reports current.</summary>
    private static async Task IndexAsync(ApiFactory factory, HttpClient owner, params Guid[] ids)
    {
        using var host = new SearchIndexWorker(factory.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<SearchIndexWorker>.Instance);
        await host.StartAsync(default);
        try
        {
            foreach (var id in ids)
            {
                JsonElement detail = default;
                for (var attempt = 0; attempt < 100; attempt++)
                {
                    detail = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{id}");
                    if (detail.GetProperty("indexStatus").GetString() == "current") break;
                    await Task.Delay(100);
                }
                Assert.Equal("current", detail.GetProperty("indexStatus").GetString());
            }
        }
        finally { await host.StopAsync(default); }
    }

    private static float[] Axis(int axis) => ControlledEmbeddingTransport.Unit(axis);

    private static float[] Blend(params (int Axis, float Weight)[] parts)
    {
        var vector = new float[ControlledEmbeddingTransport.Dimensions];
        foreach (var (axis, weight) in parts) vector[axis] = weight;
        return vector;
    }

    /// <summary>Documents naming a seeded location embed to its vector; anything else (the query) embeds to axis one.</summary>
    private static ControlledEmbeddingTransport Embeddings(params (string Marker, float[] Vector)[] documents) => new((body, _) =>
    {
        var input = JsonSerializer.Deserialize<JsonElement>(body).GetProperty("input").GetString() ?? "";
        var match = documents.FirstOrDefault(document => input.StartsWith(document.Marker + "\n", StringComparison.Ordinal) || input == document.Marker);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { model = "bge-m3", embeddings = new[] { match.Vector ?? Axis(1) } })
        });
    });

    private ApiFactory Factory(HttpMessageHandler embeddings) => new(database.ConnectionString, database.MediaRoot)
    {
        EmbeddingTransport = embeddings,
        Settings = new Dictionary<string, string?> { ["Embeddings:Endpoint"] = "http://ollama.test:11434" }
    };
}
