// Given a private inspiration image, when it is saved, then its manual metadata
// and sanitized media persist independently of My Work and background analysis.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Application.Maintenance;
using Loupe.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetVips;
using Xunit;

namespace Loupe.Api.Tests.References;

public sealed class UploadReferenceTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_009_1_3_Reference_and_metadata_survive_restart_without_a_My_Work_record_or_analysis()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        var subject = Guid.NewGuid().ToString();
        using var client = await factory.CreateAuthenticatedClientAsync(subject);
        using var response = await ReferenceFixture.SubmitAsync(client, new Dictionary<string, string>
        {
            ["title"] = "  Light study  ",
            ["sourceUrl"] = " https://example.com/photo?view=1#source ",
            ["attribution"] = "  Supplied photographer  ",
            ["notes"] = " First line\r\nSecond line "
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var saved = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Light study", saved.GetProperty("title").GetString());
        Assert.Equal("https://example.com/photo?view=1#source", saved.GetProperty("sourceUrl").GetString());
        Assert.Equal("Supplied photographer", saved.GetProperty("attribution").GetString());
        Assert.Equal("First line\nSecond line", saved.GetProperty("notes").GetString());
        Assert.Equal(1, saved.GetProperty("revision").GetInt64());
        Assert.Equal(8, saved.GetProperty("width").GetInt32());
        Assert.Equal(6, saved.GetProperty("height").GetInt32());
        await using var restarted = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var later = await restarted.CreateAuthenticatedClientAsync(subject);
        var revisited = await later.GetFromJsonAsync<JsonElement>(response.Headers.Location);
        Assert.Equal(saved.GetRawText(), revisited.GetRawText());
        foreach (var field in new[] { "imageUrl", "previewUrl" })
        {
            using var media = await later.GetAsync(saved.GetProperty(field).GetString());
            Assert.Equal(HttpStatusCode.OK, media.StatusCode);
            using var decoded = Image.NewFromBuffer(await media.Content.ReadAsByteArrayAsync());
            Assert.Equal(8, decoded.Width); Assert.Equal(6, decoded.Height);
        }
        var photographs = await later.GetFromJsonAsync<JsonElement>("/api/photographs");
        Assert.Empty(photographs.GetProperty("items").EnumerateArray());
        await using var scope = restarted.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<LibraryDbContext>().BackgroundOperations.AnyAsync());
    }

    [Fact]
    public async Task L2_009_2_Absent_metadata_remains_unknown_and_the_filename_supplies_the_title()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var response = await ReferenceFixture.SubmitAsync(client);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var saved = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Reference", saved.GetProperty("title").GetString());
        foreach (var field in new[] { "sourceUrl", "attribution", "notes" }) Assert.Equal(JsonValueKind.Null, saved.GetProperty(field).ValueKind);
    }

    [Theory]
    [InlineData("title", 201)]
    [InlineData("attribution", 201)]
    [InlineData("notes", 10001)]
    [InlineData("sourceUrl", 2049)]
    public async Task L2_009_4_Overlong_metadata_is_rejected_before_media_is_written(string field, int length)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var before = Directory.Exists(database.MediaRoot) ? Directory.GetFiles(database.MediaRoot).Length : 0;
        using var response = await ReferenceFixture.SubmitAsync(client, new Dictionary<string, string> { [field] = new string('x', length) });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(error.GetProperty("errors").TryGetProperty(field, out _));
        Assert.Equal(before, Directory.Exists(database.MediaRoot) ? Directory.GetFiles(database.MediaRoot).Length : 0);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("/relative/photo")]
    [InlineData("https://user:secret@example.com/photo")]
    [InlineData("https://example.com:8443/photo")]
    public async Task L2_009_4_Unsafe_source_URL_forms_return_a_field_error(string source)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var response = await ReferenceFixture.SubmitAsync(client, new Dictionary<string, string> { ["sourceUrl"] = source });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("sourceUrl", out _));
    }

    [Fact]
    public async Task L2_030_Reference_upload_replays_one_record_and_rejects_changed_metadata_for_the_same_key()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var key = Guid.NewGuid().ToString();
        using var first = await ReferenceFixture.SubmitAsync(client, key: key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var replay = await ReferenceFixture.SubmitAsync(client, key: key);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        using var changed = await ReferenceFixture.SubmitAsync(client, new Dictionary<string, string> { ["notes"] = "Changed notes" }, key);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        var retained = await client.GetFromJsonAsync<JsonElement>(first.Headers.Location);
        Assert.Equal(JsonValueKind.Null, retained.GetProperty("notes").ValueKind);
    }

    [Fact]
    public async Task L2_038_Reference_details_and_both_images_are_owner_scoped()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, BaseAddress = new Uri("https://localhost") });
        using var response = await ReferenceFixture.SubmitAsync(owner);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        foreach (var suffix in new[] { "", "/image", "/preview" })
        {
            using var foreign = await stranger.GetAsync(response.Headers.Location + suffix);
            using var missing = await stranger.GetAsync($"/api/references/{Guid.NewGuid()}{suffix}");
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.Equal((await missing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString(),
                (await foreign.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            using var denied = await anonymous.GetAsync(response.Headers.Location + suffix);
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
    }

    [Fact]
    public async Task L2_009_1_032_Live_reference_media_survives_abandoned_file_cleanup()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var response = await ReferenceFixture.SubmitAsync(client);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var saved = await response.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var path in Directory.GetFiles(database.MediaRoot)) File.SetLastWriteTimeUtc(path, factory.Clock.GetUtcNow().UtcDateTime - TimeSpan.FromHours(2));
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAbandonedMediaCleaner>().CleanAsync(CancellationToken.None);
        using var preview = await client.GetAsync(saved.GetProperty("previewUrl").GetString());
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var decoded = Image.NewFromBuffer(await preview.Content.ReadAsByteArrayAsync());
        Assert.Equal(8, decoded.Width);
    }
}
