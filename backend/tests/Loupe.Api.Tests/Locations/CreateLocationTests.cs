using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.Locations;

// Acceptance Test
// Traces to: L2-055
// Description: Save a location with only a name; it persists privately with address,
// images, and report absent, and never surfaces in the other library areas. Details
// at their limits are accepted, one step beyond is a field error, and hostile text
// stays inert.
public sealed class CreateLocationTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    // L2-055, criterion 1: a name-only location persists with Address not recorded, No images, and No scouting report.
    [Fact]
    public async Task L2_055_1_A_named_location_persists_with_absent_address_images_and_report()
    {
        var subject = Guid.NewGuid().ToString(); Guid id;
        await using (var factory = new ApiFactory(database.ConnectionString, database.MediaRoot))
        {
            using var owner = await factory.CreateAuthenticatedClientAsync(subject);
            using var saved = await Save(owner, new { name = "  Kew Bridge foreshore  " }, "first");
            Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
            var created = await saved.Content.ReadFromJsonAsync<JsonElement>(); id = created.GetProperty("id").GetGuid();
            Assert.Equal($"/api/locations/{id}", saved.Headers.Location?.ToString());
            AssertNameOnly(created);
            using var replay = await Save(owner, new { name = "  Kew Bridge foreshore  " }, "first"); replay.EnsureSuccessStatusCode();
            Assert.Equal(id, (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
            using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
            using var hidden = await stranger.GetAsync($"/api/locations/{id}"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        }
        await using var restart = new ApiFactory(database.ConnectionString, database.MediaRoot); using var later = await restart.CreateAuthenticatedClientAsync(subject);
        AssertNameOnly(await later.GetFromJsonAsync<JsonElement>($"/api/locations/{id}"));
        Assert.Empty((await later.GetFromJsonAsync<JsonElement>("/api/photographs")).GetProperty("items").EnumerateArray());
        Assert.Empty((await later.GetFromJsonAsync<JsonElement>("/api/references")).GetProperty("items").EnumerateArray());
        Assert.Empty((await later.GetFromJsonAsync<JsonElement>("/api/photographers")).GetProperty("items").EnumerateArray());
        Assert.Empty((await later.GetFromJsonAsync<JsonElement>("/api/search?query=Kew")).GetProperty("items").EnumerateArray());
    }

    // L2-055, criterion 3: each text field at its maximum length succeeds and one character above it names the field.
    [Theory]
    [InlineData("name", 200)]
    [InlineData("addressLine1", 200)]
    [InlineData("addressLine2", 200)]
    [InlineData("locality", 100)]
    [InlineData("region", 100)]
    [InlineData("postalCode", 20)]
    [InlineData("country", 100)]
    [InlineData("scoutingBrief", 2000)]
    [InlineData("notes", 10000)]
    public async Task L2_055_3_Text_fields_accept_their_maximum_and_reject_one_character_more(string field, int maximum)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var boundary = new string('é', maximum);
        using var accepted = await Save(owner, new Dictionary<string, object> { ["name"] = "Limit", [field] = boundary });
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        var created = await accepted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(boundary, (await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{created.GetProperty("id").GetGuid()}")).GetProperty(field).GetString());
        using var rejected = await Save(owner, new Dictionary<string, object> { ["name"] = "Limit", [field] = boundary + "é" });
        await AssertFieldError(rejected, field);
    }

    // L2-055, criterion 3: coordinates hold to their ranges, six decimal places, and travel as a pair.
    [Theory]
    [InlineData("90.000000", "0", "90.000000", "0.000000")]
    [InlineData("-90", "180.000000", "-90.000000", "180.000000")]
    [InlineData("51.487213", "-180.000000", "51.487213", "-180.000000")]
    public async Task L2_055_3_Boundary_coordinates_persist_with_six_decimal_places(string latitude, string longitude, string savedLatitude, string savedLongitude)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync();
        using var accepted = await Save(owner, new { name = "Edge", coordinates = new { latitude, longitude } });
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        var created = await accepted.Content.ReadFromJsonAsync<JsonElement>();
        var coordinates = (await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{created.GetProperty("id").GetGuid()}")).GetProperty("coordinates");
        Assert.Equal(savedLatitude, coordinates.GetProperty("latitude").GetString());
        Assert.Equal(savedLongitude, coordinates.GetProperty("longitude").GetString());
    }

    [Theory]
    [InlineData("90.000001", "0", "latitude")]
    [InlineData("-90.000001", "0", "latitude")]
    [InlineData("0", "180.000001", "longitude")]
    [InlineData("0", "-180.000001", "longitude")]
    [InlineData("51.4872131", "0", "latitude")]
    [InlineData("0", "-0.2876041", "longitude")]
    [InlineData("north", "0", "latitude")]
    [InlineData("51.487213", null, "longitude")]
    [InlineData(null, "-0.287604", "latitude")]
    public async Task L2_055_3_Out_of_range_partial_or_over_precise_coordinates_name_their_field(string? latitude, string? longitude, string field)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync();
        using var rejected = await Save(owner, new { name = "Edge", coordinates = new { latitude, longitude } });
        await AssertFieldError(rejected, field);
    }

    // L2-055, criterion 3: the setting is one of Indoor, Outdoor, or Mixed, or absent.
    [Fact]
    public async Task L2_055_3_Setting_accepts_the_three_values_and_rejects_others()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync();
        foreach (var setting in new[] { "Indoor", "Outdoor", "Mixed" })
        {
            using var accepted = await Save(owner, new { name = "Setting", setting });
            Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
            Assert.Equal(setting, (await accepted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("setting").GetString());
        }
        using var blank = await Save(owner, new { name = "Setting", setting = " " });
        Assert.Equal(JsonValueKind.Null, (await blank.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("setting").ValueKind);
        using var rejected = await Save(owner, new { name = "Setting", setting = "Underwater" });
        await AssertFieldError(rejected, "setting");
    }

    // L2-055, criterion 7: markup, script, SQL, and template text in the name, address, and locality is stored and returned as plain text.
    [Fact]
    public async Task L2_055_7_Hostile_text_in_name_address_and_locality_is_stored_inert()
    {
        const string name = "<script>alert('loupe')</script> & <b>bold</b>";
        const string addressLine1 = "1 Riverside'; DROP TABLE locations; --";
        const string locality = "{{ 7 * 7 }} ${process.env} <img src=x onerror=alert(1)>";
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync();
        using var accepted = await Save(owner, new { name, addressLine1, locality });
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        var created = await accepted.Content.ReadFromJsonAsync<JsonElement>();
        var saved = await owner.GetFromJsonAsync<JsonElement>($"/api/locations/{created.GetProperty("id").GetGuid()}");
        Assert.Equal(name, saved.GetProperty("name").GetString());
        Assert.Equal(addressLine1, saved.GetProperty("addressLine1").GetString());
        Assert.Equal(locality, saved.GetProperty("locality").GetString());
        using var again = await Save(owner, new { name = "Still here" });
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    private static async Task AssertFieldError(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_request", error.GetProperty("code").GetString());
        Assert.True(error.GetProperty("errors").TryGetProperty(field, out _), $"Expected a field error naming {field}: {error.GetRawText()}");
    }

    private static void AssertNameOnly(JsonElement location)
    {
        Assert.Equal("Kew Bridge foreshore", location.GetProperty("name").GetString());
        foreach (var field in new[] { "addressLine1", "addressLine2", "locality", "region", "postalCode", "country", "coordinates", "setting", "scoutingBrief", "notes", "coverImageId", "report" })
            Assert.Equal(JsonValueKind.Null, location.GetProperty(field).ValueKind);
        Assert.Empty(location.GetProperty("tags").EnumerateArray()); Assert.Empty(location.GetProperty("images").EnumerateArray());
        Assert.Equal("None", location.GetProperty("reportStatus").GetString()); Assert.Equal(1, location.GetProperty("revision").GetInt64());
        Assert.Equal(location.GetProperty("createdAt").GetDateTimeOffset(), location.GetProperty("updatedAt").GetDateTimeOffset());
    }

    private static async Task<HttpResponseMessage> Save(HttpClient owner, object input, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/locations") { Content = JsonContent.Create(input) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString()); return await owner.SendAsync(request);
    }
}
