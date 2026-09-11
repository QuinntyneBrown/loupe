using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.References;

public sealed class ReferenceAutomaticAnalysisTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private ApiFactory Factory() => new(database.ConnectionString, database.MediaRoot)
    { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only" } };

    // Given a configured visual provider, saving a validated image queues analysis atomically, once per image.
    [Fact]
    public async Task Upload_and_replacement_queue_analysis_and_keyed_retries_do_not_queue_duplicates()
    {
        await using var factory = Factory(); using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var key = Guid.NewGuid().ToString();
        using var upload = await ReferenceFixture.SubmitAsync(owner, new Dictionary<string, string> { ["notes"] = "Keep these" }, key: key);
        upload.EnsureSuccessStatusCode(); var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var current = await owner.GetAsync($"/api/references/{id}/analysis"); Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        var first = await current.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal("Queued", first.GetProperty("status").GetString());
        using var replay = await ReferenceFixture.SubmitAsync(owner, new Dictionary<string, string> { ["notes"] = "Keep these" }, key: key); replay.EnsureSuccessStatusCode();
        Assert.Equal(first.GetRawText(), (await owner.GetFromJsonAsync<JsonElement>($"/api/references/{id}/analysis")).GetRawText());
        var replacementKey = Guid.NewGuid().ToString();
        using var replacement = await ReferenceFixture.SubmitAsync(owner, new Dictionary<string, string> { ["revision"] = "1" }, key: replacementKey, path: $"/api/references/{id}/image", method: HttpMethod.Put); replacement.EnsureSuccessStatusCode();
        var second = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{id}/analysis");
        Assert.Equal("Queued", second.GetProperty("status").GetString()); Assert.NotEqual(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());
        var canceled = await owner.GetFromJsonAsync<JsonElement>($"/api/operations/{first.GetProperty("id").GetGuid()}"); Assert.Equal("Canceled", canceled.GetProperty("status").GetString());
        using var repeatReplacement = await ReferenceFixture.SubmitAsync(owner, new Dictionary<string, string> { ["revision"] = "1" }, key: replacementKey, path: $"/api/references/{id}/image", method: HttpMethod.Put); repeatReplacement.EnsureSuccessStatusCode();
        Assert.Equal(second.GetRawText(), (await owner.GetFromJsonAsync<JsonElement>($"/api/references/{id}/analysis")).GetRawText());
        var reference = await owner.GetFromJsonAsync<JsonElement>($"/api/references/{id}"); Assert.Equal("Keep these", reference.GetProperty("notes").GetString());
    }

    [Fact]
    public async Task A_preview_does_not_save_or_analyze_until_final_save_and_duplicate_save_reuses_analysis()
    {
        await using var factory = Factory(); using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var upload = await ReferenceFixture.SubmitAsync(owner, path: "/api/reference-drafts/images"); upload.EnsureSuccessStatusCode();
        var draftId = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Empty((await owner.GetFromJsonAsync<JsonElement>("/api/references")).GetProperty("items").EnumerateArray());
        Guid? operationId = null;
        var key = Guid.NewGuid().ToString();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/reference-drafts/{draftId}/save") { Content = JsonContent.Create(new { revision = 1, title = "Saved preview", boardIds = Array.Empty<Guid>() }) };
            request.Headers.Add("Idempotency-Key", key); using var save = await owner.SendAsync(request); save.EnsureSuccessStatusCode();
            var id = (await save.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reference").GetProperty("id").GetGuid();
            using var current = await owner.GetAsync($"/api/references/{id}/analysis"); Assert.Equal(HttpStatusCode.OK, current.StatusCode);
            var operation = await current.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal("Queued", operation.GetProperty("status").GetString());
            if (operationId is not null) Assert.Equal(operationId, operation.GetProperty("id").GetGuid());
            operationId = operation.GetProperty("id").GetGuid();
        }
    }

    [Fact]
    public async Task A_full_analysis_queue_does_not_prevent_saving_an_image_and_can_be_retried_later()
    {
        await using var factory = Factory(); using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var ids = new List<Guid>();
        for (var index = 0; index < 6; index++)
        {
            using var upload = await ReferenceFixture.SubmitAsync(owner); upload.EnsureSuccessStatusCode();
            var id = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(); ids.Add(id);
            using var current = await owner.GetAsync($"/api/references/{id}/analysis"); Assert.Equal(HttpStatusCode.OK, current.StatusCode);
            var operation = await current.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(index < 5 ? "Queued" : "Failed", operation.GetProperty("status").GetString());
            if (index == 5) Assert.Equal("analysis_limit", operation.GetProperty("failureCode").GetString());
        }
        using var deleted = await owner.DeleteAsync($"/api/references/{ids[0]}?revision=1"); deleted.EnsureSuccessStatusCode();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/references/{ids[5]}/analysis") { Content = JsonContent.Create(new { revision = 1 }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString()); using var retry = await owner.SendAsync(request); Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode);
        Assert.Equal("Queued", (await retry.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }
}
