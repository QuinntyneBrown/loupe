using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Loupe.Api.Tests.Locations;
using Loupe.Api.Tests.Photographs;
using Loupe.Api.Tests.References;
using Loupe.Api.Tests.Scouting;
using Loupe.Application.Scouting;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.ShootPlanning;

// Acceptance Test
// Traces to: L2-062, L2-061
// Description: Find a location by keyword over every eligible field including the
// current report's text, narrow the results with shoot-type, people, time-of-day,
// setting and tag filters that combine by AND before pagination, keep report-less
// locations findable only through their manual fields, and never return anything
// but the owner's locations.
public sealed class FindLocationsKeywordTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private readonly Queue<JsonObject> reports = new();

    // L2-062, criterion 1: every token must match an eligible field after NFC and case folding; a report-less location matches only manual fields.
    [Fact]
    public async Task L2_062_1_Every_token_matches_an_eligible_field_case_insensitively_after_NFC()
    {
        await using var factory = Factory();
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var reported = await CreateAsync(owner, new
        {
            name = "Kew Bridge Café foreshore",
            addressLine1 = "Thames Path",
            locality = "Brentford",
            region = "Greater London",
            postalCode = "TW8 0EF",
            country = "United Kingdom",
            setting = "Outdoor",
            scoutingBrief = "Low tide couples under the arches.",
            notes = "Parking on Kew Green.",
            tags = new[] { new { name = "riverside", category = "subject" } }
        });
        var report = ScoutingReportFixture.Valid(1);
        report["cautions"]![0]!["caution"] = "Slippery stones near the waterline.";
        await PublishAsync(factory, owner, reported, report);
        var manual = await CreateAsync(owner, new { name = "Nunhead gate", notes = "Slippery when wet.", tags = new[] { new { name = "cemetery", category = (string?)null } } });

        foreach (var query in new[] { "kew", "thames path", "brentford", "greater london", "tw8", "kingdom", "outdoor", "low tide", "parking", "riverside", "arches", "café" })
            Assert.Equal([reported], await Ids(owner, $"query={Uri.EscapeDataString(query)}"));
        Assert.Equal([reported], await Ids(owner, "query=kew+slippery"));
        Assert.Equal([manual], await Ids(owner, "query=nunhead+slippery"));
        Assert.Equal([reported, manual], (await Ids(owner, "query=slippery")).OrderBy(id => id == reported ? 0 : 1).ToArray());
        Assert.Empty(await Ids(owner, "query=kew+missing"));
        Assert.Empty(await Ids(owner, "query=citedImageIds"));
        var items = await Items(owner, "query=slippery");
        Assert.Equal("None", items.Single(item => item.GetProperty("id").GetGuid() == manual).GetProperty("reportStatus").GetString());
        Assert.Equal("Ready", items.Single(item => item.GetProperty("id").GetGuid() == reported).GetProperty("reportStatus").GetString());
    }

    // L2-061, criterion 2: every selected shoot type must be Well suited or Workable.
    [Fact]
    public async Task L2_061_2_Selected_shoot_types_combine_by_AND_over_Well_suited_or_Workable()
    {
        await using var factory = Factory();
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var both = await PublishRatedAsync(factory, owner, "Both", ("Engagement", "Well suited"), ("Events", "Workable"));
        var eventsOnly = await PublishRatedAsync(factory, owner, "Events only", ("Engagement", "Not recommended"), ("Events", "Well suited"));
        var neither = await PublishRatedAsync(factory, owner, "Neither", ("Engagement", "Cannot assess"), ("Events", "Not recommended"));
        Assert.Equal([both], await Ids(owner, "shootTypes=Engagement&shootTypes=Events"));
        Assert.Equal([eventsOnly, both], (await Ids(owner, "shootTypes=Events")).OrderBy(id => id == eventsOnly ? 0 : 1).ToArray());
        Assert.Equal(3, (await Ids(owner, "")).Length);
        var item = (await Items(owner, "shootTypes=Engagement&shootTypes=Events")).Single();
        Assert.Equal("Well suited", item.GetProperty("suitability").EnumerateArray().Single(entry => entry.GetProperty("shootType").GetString() == "Engagement").GetProperty("rating").GetString());
        Assert.NotEqual(neither, item.GetProperty("id").GetGuid());
    }

    // L2-061, criterion 3: a people count keeps only ranges containing it and never Cannot assess; clearing it restores all.
    [Fact]
    public async Task L2_061_3_A_people_count_keeps_only_group_ranges_containing_it()
    {
        await using var factory = Factory();
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var small = await PublishGroupAsync(factory, owner, "Small", 1, 2);
        var medium = await PublishGroupAsync(factory, owner, "Medium", 2, 8);
        var large = await PublishGroupAsync(factory, owner, "Large", 20, 100);
        var unknown = await PublishGroupAsync(factory, owner, "Unknown", null, null);
        Assert.Equal([medium], await Ids(owner, "people=6"));
        Assert.Equal([small, medium], (await Ids(owner, "people=2")).OrderBy(id => id == small ? 0 : 1).ToArray());
        Assert.Equal(4, (await Ids(owner, "")).Length);
        var item = (await Items(owner, "people=6")).Single();
        Assert.Equal(2, item.GetProperty("groupSize").GetProperty("minimum").GetInt32());
        Assert.Equal(8, item.GetProperty("groupSize").GetProperty("maximum").GetInt32());
        var assessed = (await Items(owner, "")).Single(entry => entry.GetProperty("id").GetGuid() == unknown);
        Assert.True(assessed.GetProperty("groupSize").GetProperty("cannotAssess").GetBoolean());
        Assert.NotEqual(large, item.GetProperty("id").GetGuid());
    }

    // L2-061, criterion 4: selected periods combine by OR over Recommended periods, without duplicates.
    [Fact]
    public async Task L2_061_4_Selected_times_of_day_combine_by_OR_over_Recommended_periods()
    {
        await using var factory = Factory();
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var golden = await PublishPeriodsAsync(factory, owner, "Golden", "Golden hour");
        var blue = await PublishPeriodsAsync(factory, owner, "Blue", "Blue hour");
        var bothPeriods = await PublishPeriodsAsync(factory, owner, "Both", "Golden hour", "Blue hour");
        var neither = await PublishPeriodsAsync(factory, owner, "Neither", "Morning");
        var found = await Ids(owner, "timesOfDay=Golden+hour&timesOfDay=Blue+hour");
        Assert.Equal(3, found.Length);
        Assert.Equal(found.Distinct().Count(), found.Length);
        Assert.DoesNotContain(neither, found);
        Assert.Contains(golden, found);
        Assert.Contains(blue, found);
        var item = (await Items(owner, "timesOfDay=Golden+hour&timesOfDay=Blue+hour")).Single(entry => entry.GetProperty("id").GetGuid() == bothPeriods);
        Assert.Equal(["Golden hour", "Blue hour"], item.GetProperty("recommendedPeriods").EnumerateArray().Select(value => value.GetString()!).ToArray());
    }

    // L2-062, criterion 2; L2-061, criterion 5: setting and tags combine by AND, report filters exclude report-less locations, and invalid filters are field errors.
    [Fact]
    public async Task L2_062_2_L2_061_5_Setting_and_tags_combine_and_report_filters_exclude_report_less_locations()
    {
        await using var factory = Factory();
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var reported = await CreateAsync(owner, new { name = "Reported", setting = "Outdoor", tags = new[] { new { name = "arches", category = (string?)null }, new { name = "river", category = (string?)null } } });
        await PublishAsync(factory, owner, reported, ScoutingReportFixture.Valid(1));
        var manual = await CreateAsync(owner, new { name = "Manual", setting = "Outdoor", tags = new[] { new { name = "arches", category = (string?)null } } });
        var indoor = await CreateAsync(owner, new { name = "Indoor", setting = "Indoor", tags = new[] { new { name = "arches", category = (string?)null }, new { name = "river", category = (string?)null } } });

        Assert.Equal(2, (await Ids(owner, "setting=Outdoor")).Length);
        Assert.Equal(3, (await Ids(owner, "tags=arches")).Length);
        Assert.Equal([reported, indoor], (await Ids(owner, "tags=arches&tags=river")).OrderBy(id => id == reported ? 0 : 1).ToArray());
        Assert.Equal([reported], await Ids(owner, "tags=ARCHES&tags=river&setting=Outdoor"));
        Assert.Equal([reported], await Ids(owner, "shootTypes=Portraits"));
        Assert.Equal([reported], await Ids(owner, "people=3&setting=Outdoor"));
        Assert.Equal([reported], await Ids(owner, "timesOfDay=Golden+hour&tags=arches"));
        Assert.Equal([manual, reported], (await Ids(owner, "query=&setting=Outdoor")).OrderBy(id => id == manual ? 0 : 1).ToArray());

        foreach (var (query, field) in new[]
        {
            (string.Join("&", Enumerable.Range(1, 11).Select(index => $"tags=tag{index}")), "tags"),
            ("shootTypes=Weddings", "shootTypes"),
            ("timesOfDay=Noon", "timesOfDay"),
            ("setting=Underwater", "setting"),
            ("people=0", "people"),
            ("people=501", "people"),
            ("query=" + new string('a', 501), "query"),
            ("pageSize=0", "pageSize"),
            ("mode=semantic", "mode"),
        })
        {
            using var rejected = await owner.GetAsync("/api/locations/search?" + query);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            var error = await rejected.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(error.GetProperty("errors").TryGetProperty(field, out _), $"{query}: {error.GetRawText()}");
        }
    }

    // L2-062, criteria 3, 5 and 7: only the owner's live locations are returned, edits show on the next read, cursors are scoped, and inspiration search never returns a location.
    [Fact]
    public async Task L2_062_3_5_7_Only_the_owners_live_locations_are_returned_and_edits_show_at_once()
    {
        await using var factory = Factory();
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var mine = new List<Guid>();
        for (var index = 0; index < 3; index++)
        {
            mine.Add(await CreateAsync(owner, new { name = $"Kew {index}" }));
            if (index == 0) factory.Clock.Advance(TimeSpan.FromSeconds(1));
        }
        await CreateAsync(stranger, new { name = "Kew stranger" });
        using var reference = await ReferenceFixture.SubmitAsync(owner, new Dictionary<string, string> { ["title"] = "Kew reference" });
        reference.EnsureSuccessStatusCode();
        await PhotographFixture.UploadAsync(owner, "Kew photograph");
        var found = await Ids(owner, "query=kew");
        Assert.Equal(3, found.Length);
        Assert.All(found, id => Assert.Contains(id, mine));
        var inspiration = (await owner.GetFromJsonAsync<JsonElement>("/api/search?query=Kew")).GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(["reference"], inspiration.Select(item => item.GetProperty("type").GetString()!).Distinct().ToArray());
        Assert.Single(inspiration);

        var edited = mine[1];
        var current = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{edited}");
        using var edit = await owner.PutAsJsonAsync($"/api/locations/{edited}/notes", new { revision = current.GetProperty("revision").GetInt64(), text = "Heron at dawn." });
        edit.EnsureSuccessStatusCode();
        Assert.Equal([edited], await Ids(owner, "query=heron"));

        var deleted = mine[2];
        var latest = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{deleted}");
        using var removal = await owner.DeleteAsync($"/api/locations/{deleted}?revision={latest.GetProperty("revision").GetInt64()}");
        removal.EnsureSuccessStatusCode();
        var page = await owner.GetFromJsonAsync<JsonElement>("/api/locations/search?query=kew&pageSize=1");
        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
        Assert.Single(page.GetProperty("items").EnumerateArray());
        var cursor = Uri.EscapeDataString(page.GetProperty("nextCursor").GetString()!);
        var second = await owner.GetFromJsonAsync<JsonElement>("/api/locations/search?query=kew&pageSize=1&cursor=" + cursor);
        Assert.Single(second.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, second.GetProperty("nextCursor").ValueKind);
        Assert.DoesNotContain(deleted, page.GetProperty("items").EnumerateArray().Concat(second.GetProperty("items").EnumerateArray()).Select(item => item.GetProperty("id").GetGuid()));
        using var foreignCursor = await owner.GetAsync("/api/locations/search?query=kew&pageSize=2&cursor=" + cursor);
        Assert.Equal(HttpStatusCode.BadRequest, foreignCursor.StatusCode);
        using var strangerCursor = await stranger.GetAsync("/api/locations/search?query=kew&pageSize=1&cursor=" + cursor);
        Assert.Equal(HttpStatusCode.BadRequest, strangerCursor.StatusCode);
    }

    // L2-061 tag filter: the filter choices are the owner's active location tags, grouped by identity and counted, never a reference's tags or a stranger's.
    [Fact]
    public async Task L2_061_Tag_filter_choices_list_the_owners_location_tags_by_count()
    {
        await using var factory = Factory();
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        await CreateAsync(owner, new { name = "Kew", tags = new[] { new { name = "river", category = (string?)"subject" }, new { name = "low tide", category = (string?)null } } });
        await CreateAsync(owner, new { name = "Putney", tags = new[] { new { name = "River", category = (string?)null } } });
        await CreateAsync(stranger, new { name = "Elsewhere", tags = new[] { new { name = "rooftop", category = (string?)null } } });
        using var reference = await ReferenceFixture.SubmitAsync(owner, new Dictionary<string, string> { ["title"] = "Kew reference" });
        reference.EnsureSuccessStatusCode();
        var referenceId = (await reference.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var tagged = await owner.PutAsJsonAsync($"/api/references/{referenceId}/tags", new { revision = 1, tags = new[] { new { name = "portrait" } } });
        tagged.EnsureSuccessStatusCode();

        var tags = (await owner.GetFromJsonAsync<JsonElement>("/api/locations/search/tags")).EnumerateArray().ToArray();
        Assert.Equal(["RIVER", "LOW TIDE"], tags.Select(tag => tag.GetProperty("normalizedName").GetString()!).ToArray());
        Assert.Equal([2, 1], tags.Select(tag => tag.GetProperty("count").GetInt32()).ToArray());
        Assert.Equal("River", tags[0].GetProperty("name").GetString());
        Assert.Equal("low tide", tags[1].GetProperty("name").GetString());
        Assert.Equal(["ROOFTOP"], (await stranger.GetFromJsonAsync<JsonElement>("/api/locations/search/tags")).EnumerateArray().Select(tag => tag.GetProperty("normalizedName").GetString()!).ToArray());
    }

    private ApiFactory Factory() => new(database.ConnectionString, database.MediaRoot)
    {
        Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only" },
        AiTransport = new ControlledSourceTransport((_, _) => Task.FromResult(ScoutingReportFixture.Output(reports.Dequeue())))
    };

    private static async Task<Guid> CreateAsync(HttpClient owner, object input) => (await LocationFixture.CreateAsync(owner, input)).GetProperty("id").GetGuid();

    private static async Task<JsonElement[]> Items(HttpClient owner, string query)
    {
        using var response = await owner.GetAsync("/api/locations/search?" + query);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<JsonElement>(body).GetProperty("items").EnumerateArray().ToArray();
    }

    private static async Task<Guid[]> Ids(HttpClient owner, string query) => (await Items(owner, query)).Select(item => item.GetProperty("id").GetGuid()).ToArray();

    /// <summary>Publishes a report through the real admission and worker path with the canned provider result.</summary>
    private async Task PublishAsync(ApiFactory factory, HttpClient owner, Guid id, JsonObject report)
    {
        var location = await LocationFixture.AddImageAsync(owner, id);
        reports.Enqueue(report);
        using var admitted = await ScoutingReportFixture.RequestAsync(owner, id, location.GetProperty("revision").GetInt64());
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunScoutingReportCommand()));
        var operation = await owner.GetFromJsonAsync<JsonElement>(admitted.Headers.Location);
        Assert.Equal("Succeeded", operation.GetProperty("status").GetString());
    }

    private async Task<Guid> PublishRatedAsync(ApiFactory factory, HttpClient owner, string name, params (string ShootType, string Rating)[] ratings)
    {
        var id = await CreateAsync(owner, new { name });
        var report = ScoutingReportFixture.Valid(1);
        foreach (var entry in report["suitability"]!.AsArray())
            foreach (var (shootType, rating) in ratings)
                if (entry!["shootType"]!.GetValue<string>() == shootType) entry["rating"] = rating;
        await PublishAsync(factory, owner, id, report);
        return id;
    }

    private async Task<Guid> PublishGroupAsync(ApiFactory factory, HttpClient owner, string name, int? minimum, int? maximum)
    {
        var id = await CreateAsync(owner, new { name });
        var report = ScoutingReportFixture.Valid(1);
        report["groupSize"]!["cannotAssess"] = minimum is null;
        report["groupSize"]!["minimum"] = minimum;
        report["groupSize"]!["maximum"] = maximum;
        await PublishAsync(factory, owner, id, report);
        return id;
    }

    private async Task<Guid> PublishPeriodsAsync(ApiFactory factory, HttpClient owner, string name, params string[] recommended)
    {
        var id = await CreateAsync(owner, new { name });
        var report = ScoutingReportFixture.Valid(1);
        foreach (var entry in report["timesOfDay"]!.AsArray())
        {
            var period = entry!["period"]!.GetValue<string>();
            entry["rating"] = recommended.Contains(period) ? "Recommended" : period == "Midday" ? "Avoid" : "Unknown";
            entry["basis"] = recommended.Contains(period) || period == "Midday" ? "Visible" : "Inferred";
        }
        await PublishAsync(factory, owner, id, report);
        return id;
    }
}
