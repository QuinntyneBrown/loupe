using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.Locations;

// Acceptance Test
// Traces to: L2-055
// Description: Save a location with only a name; it persists privately with address,
// images, and report absent, and never surfaces in the other library areas.
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
