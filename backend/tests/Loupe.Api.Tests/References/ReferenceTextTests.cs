using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.References;

public sealed class ReferenceTextTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Description_and_notes_save_independently_and_clearing_description_removes_it()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var upload = await ReferenceFixture.SubmitAsync(owner, new Dictionary<string, string> { ["notes"] = "Keep my notes" });
        var initial = await upload.Content.ReadFromJsonAsync<JsonElement>(); var id = initial.GetProperty("id").GetGuid();
        using var description = await owner.PutAsJsonAsync($"/api/references/{id}/description", new { revision = 1, text = "  Warm backlight.\r\nOpen space.  " });
        Assert.Equal(HttpStatusCode.OK, description.StatusCode);
        var described = await description.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Warm backlight.\nOpen space.", described.GetProperty("description").GetString());
        Assert.Equal("manual", described.GetProperty("descriptionProvenance").GetString());
        Assert.Equal("Keep my notes", described.GetProperty("notes").GetString());
        using var notes = await owner.PutAsJsonAsync($"/api/references/{id}/notes", new { revision = 2, text = "  Try this light.  " }); Assert.Equal(HttpStatusCode.OK, notes.StatusCode);
        var noted = await notes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(described.GetProperty("description").GetString(), noted.GetProperty("description").GetString());
        Assert.Equal("Try this light.", noted.GetProperty("notes").GetString());
        foreach (var field in new[] { "title", "createdAt", "sourceUrl", "attribution", "imageUrl", "previewUrl" }) Assert.Equal(initial.GetProperty(field).ToString(), noted.GetProperty(field).ToString());
        using var clear = await owner.PutAsJsonAsync($"/api/references/{id}/description", new { revision = 3, text = " " }); Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        var saved = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{id}");
        Assert.Equal(JsonValueKind.Null, saved.GetProperty("description").ValueKind); Assert.Equal("Try this light.", saved.GetProperty("notes").GetString());
        Assert.Equal(4, saved.GetProperty("revision").GetInt64());
    }

    [Theory]
    [InlineData("description", 4000)]
    [InlineData("notes", 10000)]
    public async Task Text_limits_ownership_and_revision_are_enforced(string field, int maximum)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var upload = await ReferenceFixture.SubmitAsync(owner); var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var url = $"/api/references/{id}/{field}";
        using var foreign = await stranger.PutAsJsonAsync(url, new { revision = 1, text = "Secret" }); Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var stale = await owner.PutAsJsonAsync(url, new { revision = 2, text = "Stale" }); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var overlong = await owner.PutAsJsonAsync(url, new { revision = 1, text = new string('a', maximum + 1) }); Assert.Equal(HttpStatusCode.BadRequest, overlong.StatusCode);
        using var boundary = await owner.PutAsJsonAsync(url, new { revision = 1, text = new string('a', maximum) }); Assert.Equal(HttpStatusCode.OK, boundary.StatusCode);
        Assert.Equal(maximum, (await owner.GetFromJsonAsync<JsonElement>($"/api/references/{id}")).GetProperty(field).GetString()!.Length);
    }
}
