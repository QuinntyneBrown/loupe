// Given an owned saved photograph and explicit analysis mode, when critique is
// admitted, then a private durable operation survives restart and deletion cancels it.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Photographs;
using Xunit;

namespace Loupe.Api.Tests.Critiques;

public sealed class CritiqueAdmissionTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private ApiFactory CreateFactory(string? mode = "Demo") => new(database.ConnectionString, database.MediaRoot)
    {
        Settings = new Dictionary<string, string?> { ["Ai:Mode"] = mode }
    };

    [Fact]
    public async Task L2_008_1_033_1_Admission_is_durable_and_status_never_exposes_private_inputs()
    {
        var subject = Guid.NewGuid().ToString();
        string location;
        string original;
        await using (var factory = CreateFactory())
        {
            using var client = await factory.CreateAuthenticatedClientAsync(subject);
            var photo = await PhotographFixture.UploadAsync(client);
            var id = photo.GetProperty("id").GetGuid();
            using var notes = await client.PutAsJsonAsync($"/api/photographs/{id}/notes", new { revision = 1, notes = "Private notes never sent to AI" });
            notes.EnsureSuccessStatusCode();
            using var admitted = await SubmitAsync(client, id, 2);
            Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
            location = admitted.Headers.Location!.OriginalString;
            original = await admitted.Content.ReadAsStringAsync();
            var operation = JsonSerializer.Deserialize<JsonElement>(original);
            Assert.Equal($"/api/operations/{operation.GetProperty("id").GetGuid()}", location);
            Assert.Equal(id, operation.GetProperty("resourceId").GetGuid());
            Assert.Equal("Critique", operation.GetProperty("type").GetString());
            Assert.Equal("Queued", operation.GetProperty("status").GetString());
            Assert.Equal("Demo", operation.GetProperty("mode").GetString());
            Assert.Equal("Waiting to start.", operation.GetProperty("message").GetString());
            Assert.True(operation.GetProperty("createdAt").GetDateTimeOffset() <= factory.Clock.GetUtcNow());
            Assert.DoesNotContain("Private notes", original);
            Assert.DoesNotContain("imageKey", original, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("snapshot", original, StringComparison.OrdinalIgnoreCase);
            using var editable = await client.PutAsJsonAsync($"/api/photographs/{id}/brief", new { revision = 2, intent = "A later brief" });
            editable.EnsureSuccessStatusCode();
        }
        await using var restarted = CreateFactory();
        using var later = await restarted.CreateAuthenticatedClientAsync(subject);
        using var status = await later.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.Equal(original, await status.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task L2_033_5_Foreign_and_unknown_items_and_operations_are_unavailable()
    {
        await using var factory = CreateFactory();
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(owner)).GetProperty("id").GetGuid();
        foreach (var target in new[] { id, Guid.NewGuid() })
        {
            using var hidden = await SubmitAsync(stranger, target, 1);
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
            Assert.Equal("item_unavailable", (await hidden.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
        using var admitted = await SubmitAsync(owner, id, 1);
        Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        foreach (var location in new[] { admitted.Headers.Location!.OriginalString, $"/api/operations/{Guid.NewGuid()}" })
        {
            using var hidden = await stranger.GetAsync(location);
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
            Assert.Equal("item_unavailable", (await hidden.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Live")]
    public async Task L2_036_2_Unconfigured_analysis_does_not_admit_or_prevent_manual_library_work(string? mode)
    {
        await using var factory = CreateFactory(mode);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var unavailable = await SubmitAsync(client, id, 1);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Equal("integration_not_configured", (await unavailable.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        using var saved = await client.GetAsync($"/api/photographs/{id}");
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
    }

    [Fact]
    public async Task L2_029_2_030_Invalid_key_and_revision_do_not_admit_work()
    {
        await using var factory = CreateFactory();
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var missingKey = await client.PostAsJsonAsync($"/api/photographs/{id}/critique", new { revision = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, missingKey.StatusCode);
        using var invalidRevision = await SubmitAsync(client, id, 0);
        Assert.Equal(HttpStatusCode.BadRequest, invalidRevision.StatusCode);
        using var edited = await client.PutAsJsonAsync($"/api/photographs/{id}/notes", new { revision = 1, notes = "New notes" });
        edited.EnsureSuccessStatusCode();
        using var stale = await SubmitAsync(client, id, 1);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("revision_conflict", (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task L2_031_2_033_4_Deleting_a_queued_photograph_cancels_its_operation()
    {
        await using var factory = CreateFactory();
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var admitted = await SubmitAsync(client, id, 1);
        Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        using var deleted = await client.DeleteAsync($"/api/photographs/{id}?revision=1");
        deleted.EnsureSuccessStatusCode();
        var operation = await client.GetFromJsonAsync<JsonElement>(admitted.Headers.Location);
        Assert.Equal("Canceled", operation.GetProperty("status").GetString());
        Assert.Equal("The photograph was deleted.", operation.GetProperty("message").GetString());
        Assert.NotEqual(JsonValueKind.Null, operation.GetProperty("completedAt").ValueKind);
        using var retry = await SubmitAsync(client, id, 1);
        Assert.Equal(HttpStatusCode.NotFound, retry.StatusCode);
    }

    private static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid id, long revision)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{id}/critique")
        { Content = JsonContent.Create(new { revision, regenerate = false }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }
}
