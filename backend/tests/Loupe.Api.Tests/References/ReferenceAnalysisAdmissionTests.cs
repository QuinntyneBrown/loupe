using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.References;

public sealed class ReferenceAnalysisAdmissionTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private ApiFactory Factory(bool configured = true) => new(database.ConnectionString, database.MediaRoot)
    { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = configured ? "fixture-only" : null } };

    [Fact]
    public async Task Owned_image_admits_one_durable_analysis_without_changing_active_metadata()
    {
        var subject = Guid.NewGuid().ToString(); Guid id; string savedOperation;
        await using (var factory = Factory())
        {
            using var client = await factory.CreateAuthenticatedClientAsync(subject);
            using var upload = await ReferenceFixture.SubmitAsync(client); id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var before = await client.GetStringAsync($"/api/references/{id}"); var key = Guid.NewGuid().ToString();
            using var admitted = await Submit(client, id, 1, key); Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
            savedOperation = await admitted.Content.ReadAsStringAsync();
            var operation = JsonSerializer.Deserialize<JsonElement>(savedOperation);
            Assert.Equal("ReferenceAnalysis", operation.GetProperty("type").GetString()); Assert.Equal("Queued", operation.GetProperty("status").GetString());
            Assert.Equal(id, operation.GetProperty("resourceId").GetGuid());
            using var replay = await Submit(client, id, 1, key); Assert.Equal(savedOperation, await replay.Content.ReadAsStringAsync());
            using var duplicate = await Submit(client, id, 1); Assert.Equal(savedOperation, await duplicate.Content.ReadAsStringAsync());
            Assert.Equal(before, await client.GetStringAsync($"/api/references/{id}"));
        }
        await using var restart = Factory(); using var later = await restart.CreateAuthenticatedClientAsync(subject);
        Assert.Equal(savedOperation, await later.GetStringAsync($"/api/references/{id}/analysis"));
    }

    [Fact]
    public async Task Analysis_requires_an_owned_image_current_revision_and_live_configuration()
    {
        await using var factory = Factory(); using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var upload = await ReferenceFixture.SubmitAsync(owner); var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var foreign = await Submit(stranger, id, 1); Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var stale = await Submit(owner, id, 2); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var linkRequest = new HttpRequestMessage(HttpMethod.Post, "/api/references/links") { Content = JsonContent.Create(new { sourceUrl = "https://source.example/photo" }) };
        linkRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString()); using var link = await owner.SendAsync(linkRequest);
        var linkId = (await link.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reference").GetProperty("id").GetGuid();
        using var noImage = await Submit(owner, linkId, 1); Assert.Equal(HttpStatusCode.BadRequest, noImage.StatusCode);
        await using var disabled = Factory(false); using var disabledOwner = await disabled.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var disabledUpload = await ReferenceFixture.SubmitAsync(disabledOwner); var disabledId = (await disabledUpload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var unavailable = await Submit(disabledOwner, disabledId, 1); Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
    }

    private static async Task<HttpResponseMessage> Submit(HttpClient client, Guid id, long revision, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/references/{id}/analysis") { Content = JsonContent.Create(new { revision }) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString()); return await client.SendAsync(request);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Replacing_or_deleting_cancels_analysis_of_the_old_image(bool delete)
    {
        await using var factory = Factory(); using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var upload = await ReferenceFixture.SubmitAsync(owner); var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var admission = await Submit(owner, id, 1); admission.EnsureSuccessStatusCode();
        var operationId = (await admission.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var change = delete ? await owner.DeleteAsync($"/api/references/{id}?revision=1")
            : await ReferenceFixture.SubmitAsync(owner, new Dictionary<string, string> { ["revision"] = "1" }, path: $"/api/references/{id}/image", method: HttpMethod.Put);
        change.EnsureSuccessStatusCode();
        var operation = await owner.GetFromJsonAsync<JsonElement>($"/api/operations/{operationId}");
        Assert.Equal("Canceled", operation.GetProperty("status").GetString());
        if (!delete)
        {
            using var current = await owner.GetAsync($"/api/references/{id}/analysis"); Assert.Equal(HttpStatusCode.NoContent, current.StatusCode);
            using var replacement = await Submit(owner, id, 2); Assert.Equal(HttpStatusCode.Accepted, replacement.StatusCode);
            Assert.NotEqual(operationId, (await replacement.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        }
    }
}
