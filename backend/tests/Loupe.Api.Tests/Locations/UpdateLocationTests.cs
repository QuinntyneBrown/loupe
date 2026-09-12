using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.Locations;

// Acceptance Test
// Traces to: L2-055
// Description: Edit a location's structured details, scouting brief, notes, and tags
// through their own endpoints; every edit is normalized, revision-protected, and
// leaves the other parts of the record untouched.
public sealed class UpdateLocationTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    // L2-055, criterion 2: every optional field supplied with valid values is displayed normalized after reload.
    [Fact]
    public async Task L2_055_2_Every_optional_field_round_trips_normalized_after_editing()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var url = await CreateAsync(owner, new { name = "Kew Bridge" });
        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        using var details = await owner.PutAsJsonAsync(url, new
        {
            revision = 1,
            name = " Kew Bridge foreshore ",
            addressLine1 = " Thames Path ",
            addressLine2 = "   ",
            locality = "Brentford",
            region = "Greater London",
            postalCode = "TW8 0EF",
            country = "United Kingdom",
            coordinates = new { latitude = "51.48721", longitude = "-0.2876" },
            setting = "outdoor"
        });
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        using var brief = await owner.PutAsJsonAsync($"{url}/scouting-brief", new { revision = 2, text = "  Low tide reflections at golden hour.\r\n" });
        Assert.Equal(HttpStatusCode.OK, brief.StatusCode);
        using var notes = await owner.PutAsJsonAsync($"{url}/notes", new { revision = 3, text = "Parking on Kew Green.\r\nCheck tide tables." });
        Assert.Equal(HttpStatusCode.OK, notes.StatusCode);
        using var tags = await owner.PutAsJsonAsync($"{url}/tags", new { revision = 4, tags = new[] { new { name = " Riverside ", category = (string?)"subject" }, new { name = "Golden hour", category = (string?)null } } });
        Assert.Equal(HttpStatusCode.OK, tags.StatusCode);

        var saved = await owner.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal("Kew Bridge foreshore", saved.GetProperty("name").GetString());
        Assert.Equal("Thames Path", saved.GetProperty("addressLine1").GetString());
        Assert.Equal(JsonValueKind.Null, saved.GetProperty("addressLine2").ValueKind);
        Assert.Equal("Brentford", saved.GetProperty("locality").GetString());
        Assert.Equal("Greater London", saved.GetProperty("region").GetString());
        Assert.Equal("TW8 0EF", saved.GetProperty("postalCode").GetString());
        Assert.Equal("United Kingdom", saved.GetProperty("country").GetString());
        Assert.Equal("51.487210", saved.GetProperty("coordinates").GetProperty("latitude").GetString());
        Assert.Equal("-0.287600", saved.GetProperty("coordinates").GetProperty("longitude").GetString());
        Assert.Equal("Outdoor", saved.GetProperty("setting").GetString());
        Assert.Equal("Low tide reflections at golden hour.", saved.GetProperty("scoutingBrief").GetString());
        Assert.Equal("Parking on Kew Green.\nCheck tide tables.", saved.GetProperty("notes").GetString());
        Assert.Equal(["Golden hour", "Riverside"], saved.GetProperty("tags").EnumerateArray().Select(tag => tag.GetProperty("name").GetString()!).ToArray());
        Assert.Equal("subject", saved.GetProperty("tags").EnumerateArray().Single(tag => tag.GetProperty("name").GetString() == "Riverside").GetProperty("category").GetString());
        Assert.Equal(5, saved.GetProperty("revision").GetInt64());
        Assert.True(saved.GetProperty("updatedAt").GetDateTimeOffset() > saved.GetProperty("createdAt").GetDateTimeOffset());
    }

    // L2-055, criterion 4: editing only the structured details or the notes leaves the tags unchanged.
    [Fact]
    public async Task L2_055_4_Editing_details_or_notes_leaves_tags_untouched()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var url = await CreateAsync(owner, new { name = "Tagged", tags = new[] { new { name = "Riverside", category = "subject" }, new { name = "Mist", category = "mood" } } });
        var before = (await owner.GetFromJsonAsync<JsonElement>(url)).GetProperty("tags").GetRawText();
        using var details = await owner.PutAsJsonAsync(url, new { revision = 1, name = "Renamed", locality = "Brentford", setting = "Mixed" });
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        using var notes = await owner.PutAsJsonAsync($"{url}/notes", new { revision = 2, text = "Notes only." });
        Assert.Equal(HttpStatusCode.OK, notes.StatusCode);
        var after = await owner.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal(before, after.GetProperty("tags").GetRawText());
        Assert.Equal("Renamed", after.GetProperty("name").GetString());
        Assert.Equal("Notes only.", after.GetProperty("notes").GetString());
    }

    // L2-055, criterion 3: an invalid value on any edit names its field and leaves the saved values unchanged.
    [Theory]
    [InlineData("name", "", "name")]
    [InlineData("addressLine1", "over", "addressLine1")]
    [InlineData("setting", "Underwater", "setting")]
    [InlineData("coordinates", "lat-91", "latitude")]
    public async Task L2_055_3_An_invalid_detail_edit_changes_nothing(string field, string kind, string expectedField)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var url = await CreateAsync(owner, new { name = "Original", locality = "Brentford", coordinates = new { latitude = "51.487213", longitude = "-0.287604" } });
        var original = (await owner.GetFromJsonAsync<JsonElement>(url)).GetRawText();
        object value = kind switch
        {
            "over" => new string('a', 201),
            "lat-91" => new { latitude = "91", longitude = "0" },
            _ => kind
        };
        var input = new Dictionary<string, object> { ["revision"] = 1, ["name"] = "Changed", ["locality"] = "Changed", [field] = value };
        using var rejected = await owner.PutAsJsonAsync(url, input);
        await AssertFieldError(rejected, expectedField);
        Assert.Equal(original, (await owner.GetFromJsonAsync<JsonElement>(url)).GetRawText());
    }

    [Theory]
    [InlineData("scouting-brief", 2001, "scoutingBrief")]
    [InlineData("notes", 10001, "notes")]
    public async Task L2_055_3_An_over_long_brief_or_notes_edit_changes_nothing(string path, int length, string field)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var url = await CreateAsync(owner, new { name = "Original", scoutingBrief = "Keep", notes = "Keep" });
        var original = (await owner.GetFromJsonAsync<JsonElement>(url)).GetRawText();
        using var accepted = await owner.PutAsJsonAsync($"{url}/{path}", new { revision = 1, text = new string('b', length - 1) });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var boundary = (await owner.GetFromJsonAsync<JsonElement>(url)).GetRawText();
        Assert.NotEqual(original, boundary);
        using var rejected = await owner.PutAsJsonAsync($"{url}/{path}", new { revision = 2, text = new string('b', length) });
        await AssertFieldError(rejected, field);
        Assert.Equal(boundary, (await owner.GetFromJsonAsync<JsonElement>(url)).GetRawText());
    }

    [Fact]
    public async Task L2_055_3_More_than_fifty_tags_changes_nothing()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var url = await CreateAsync(owner, new { name = "Original", tags = new[] { new { name = "Keep", category = (string?)null } } });
        var original = (await owner.GetFromJsonAsync<JsonElement>(url)).GetRawText();
        using var rejected = await owner.PutAsJsonAsync($"{url}/tags", new { revision = 1, tags = Enumerable.Range(1, 51).Select(index => new { name = $"Tag {index}", category = (string?)null }).ToArray() });
        await AssertFieldError(rejected, "tags");
        Assert.Equal(original, (await owner.GetFromJsonAsync<JsonElement>(url)).GetRawText());
    }

    // L2-055, criterion 6: the second of two editors at the same revision receives a conflict and no partial update is applied.
    [Fact]
    public async Task L2_055_6_A_stale_revision_is_rejected_on_every_edit_without_partial_update()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var url = await CreateAsync(owner, new { name = "Shared", scoutingBrief = "First brief", notes = "First notes", tags = new[] { new { name = "Keep", category = (string?)null } } });
        using var winner = await owner.PutAsJsonAsync(url, new { revision = 1, name = "Winner", locality = "Brentford" });
        Assert.Equal(HttpStatusCode.OK, winner.StatusCode);
        var current = (await owner.GetFromJsonAsync<JsonElement>(url)).GetRawText();

        using var details = await owner.PutAsJsonAsync(url, new { revision = 1, name = "Loser", locality = "Elsewhere" });
        await AssertConflict(details);
        using var brief = await owner.PutAsJsonAsync($"{url}/scouting-brief", new { revision = 1, text = "Loser brief" });
        await AssertConflict(brief);
        using var notes = await owner.PutAsJsonAsync($"{url}/notes", new { revision = 1, text = "Loser notes" });
        await AssertConflict(notes);
        using var tags = await owner.PutAsJsonAsync($"{url}/tags", new { revision = 1, tags = new[] { new { name = "Loser", category = (string?)null } } });
        await AssertConflict(tags);
        Assert.Equal(current, (await owner.GetFromJsonAsync<JsonElement>(url)).GetRawText());

        using var reloaded = await owner.PutAsJsonAsync(url, new { revision = 2, name = "Loser after reload", locality = "Elsewhere" });
        Assert.Equal(HttpStatusCode.OK, reloaded.StatusCode);
        Assert.Equal(3, (await reloaded.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revision").GetInt64());
    }

    // L2-055: another owner's location is not found on every edit route.
    [Fact]
    public async Task L2_055_Another_owners_location_is_not_found_on_every_edit_route()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync();
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var url = await CreateAsync(owner, new { name = "Private" });
        var original = (await owner.GetFromJsonAsync<JsonElement>(url)).GetRawText();
        using var details = await stranger.PutAsJsonAsync(url, new { revision = 1, name = "Taken" });
        Assert.Equal(HttpStatusCode.NotFound, details.StatusCode);
        using var brief = await stranger.PutAsJsonAsync($"{url}/scouting-brief", new { revision = 1, text = "Taken" });
        Assert.Equal(HttpStatusCode.NotFound, brief.StatusCode);
        using var notes = await stranger.PutAsJsonAsync($"{url}/notes", new { revision = 1, text = "Taken" });
        Assert.Equal(HttpStatusCode.NotFound, notes.StatusCode);
        using var tags = await stranger.PutAsJsonAsync($"{url}/tags", new { revision = 1, tags = new[] { new { name = "Taken", category = (string?)null } } });
        Assert.Equal(HttpStatusCode.NotFound, tags.StatusCode);
        Assert.Equal(original, (await owner.GetFromJsonAsync<JsonElement>(url)).GetRawText());
    }

    private static async Task<string> CreateAsync(HttpClient owner, object input)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/locations") { Content = JsonContent.Create(input) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await owner.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return $"/api/locations/{(await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid()}";
    }

    private static async Task AssertFieldError(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_request", error.GetProperty("code").GetString());
        Assert.True(error.GetProperty("errors").TryGetProperty(field, out _), $"Expected a field error naming {field}: {error.GetRawText()}");
    }

    private static async Task AssertConflict(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("revision_conflict", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }
}
