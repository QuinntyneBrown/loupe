// Given incomplete or unsupported critique output, when a replacement runs,
// then it receives one bounded retry and never overwrites the earlier result.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Application.Critiques;
using Loupe.Api.Tests.Photographs;
using Loupe.Domain.Critiques;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.Critiques;

public sealed class CritiqueValidationTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task L2_033_4_034_1_Queued_validation_retry_can_succeed_or_be_canceled_by_deletion(bool delete)
    {
        var valid = CritiqueResultFixture.Valid();
        var provider = new ControlledCritiqueProvider((call, _, _) => Task.FromResult(call == 1 ? valid with { Exposure = null! } : valid));
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot)
        { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Demo" }, CritiqueProvider = provider };
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var admitted = await SubmitAsync(client, id, 1);
        Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        Assert.True(await RunAsync(factory));
        using var empty = await client.GetAsync($"/api/photographs/{id}/critique");
        Assert.Equal(HttpStatusCode.NoContent, empty.StatusCode);
        if (delete)
        {
            using var deleted = await client.DeleteAsync($"/api/photographs/{id}?revision=1");
            deleted.EnsureSuccessStatusCode();
        }
        factory.Clock.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(!delete, await RunAsync(factory));
        var terminal = await client.GetFromJsonAsync<JsonElement>(admitted.Headers.Location);
        Assert.Equal(delete ? "Canceled" : "Succeeded", terminal.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, terminal.GetProperty("nextAttemptAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, terminal.GetProperty("failureCode").ValueKind);
        Assert.Equal(delete ? 1 : 2, provider.Calls);
        if (!delete)
        {
            var saved = await client.GetFromJsonAsync<JsonElement>($"/api/photographs/{id}/critique");
            Assert.Equal(terminal.GetProperty("id").GetGuid(), saved.GetProperty("operationId").GetGuid());
        }
    }

    [Theory]
    [InlineData("missing-section")]
    [InlineData("blank-strength")]
    [InlineData("two-priorities")]
    [InlineData("four-priorities")]
    [InlineData("blank-action")]
    [InlineData("missing-uncertainty")]
    [InlineData("unsupported-exif")]
    [InlineData("missing-exercise")]
    public async Task L2_006_3_007_2_034_3_Invalid_replacement_retries_once_and_preserves_saved_output(string invalidCase)
    {
        var valid = CritiqueResultFixture.Valid();
        var invalid = invalidCase switch
        {
            "missing-section" => valid with { Exposure = null! },
            "blank-strength" => valid with { Strengths = [valid.Strengths[0] with { Explanation = "  " }] },
            "two-priorities" => valid with { Improvements = valid.Improvements[..2] },
            "four-priorities" => valid with { Improvements = [.. valid.Improvements, valid.Improvements[0]] },
            "blank-action" => valid with { Improvements = [valid.Improvements[0] with { Action = "" }, .. valid.Improvements[1..]] },
            "missing-uncertainty" => valid with { Focus = valid.Focus with { UncertaintyReason = null } },
            "unsupported-exif" => valid with { Exposure = valid.Exposure with { Evidence = [new(EvidenceKind.ExifFact, "Private raw provider assertion", "Camera", "Invented camera")] } },
            "missing-exercise" => valid with { Exercise = null! },
            _ => throw new ArgumentOutOfRangeException(nameof(invalidCase))
        };
        var provider = new ControlledCritiqueProvider((call, _, _) => Task.FromResult(call == 1 ? valid : invalid));
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot)
        { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Demo" }, CritiqueProvider = provider };
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var id = (await PhotographFixture.UploadAsync(client)).GetProperty("id").GetGuid();
        using var original = await SubmitAsync(client, id, 1);
        Assert.Equal(HttpStatusCode.Accepted, original.StatusCode);
        Assert.True(await RunAsync(factory));
        var saved = await client.GetStringAsync($"/api/photographs/{id}/critique");
        var photo = await client.GetFromJsonAsync<JsonElement>($"/api/photographs/{id}");
        using var replacement = await SubmitAsync(client, id, photo.GetProperty("revision").GetInt64());
        Assert.Equal(HttpStatusCode.Accepted, replacement.StatusCode);
        Assert.True(await RunAsync(factory));
        var queued = await client.GetFromJsonAsync<JsonElement>(replacement.Headers.Location);
        Assert.Equal("Queued", queued.GetProperty("status").GetString());
        Assert.InRange(queued.GetProperty("nextAttemptAt").GetDateTimeOffset(),
            factory.Clock.GetUtcNow().AddSeconds(5).AddTicks(-9), factory.Clock.GetUtcNow().AddSeconds(5));
        Assert.Equal(saved, await client.GetStringAsync($"/api/photographs/{id}/critique"));
        Assert.False(await RunAsync(factory));
        Assert.Equal(2, provider.Calls);
        factory.Clock.Advance(TimeSpan.FromSeconds(5));
        Assert.True(await RunAsync(factory));
        var failed = await client.GetFromJsonAsync<JsonElement>(replacement.Headers.Location);
        Assert.Equal("Failed", failed.GetProperty("status").GetString());
        Assert.Equal("invalid_output", failed.GetProperty("failureCode").GetString());
        Assert.Null(failed.GetProperty("nextAttemptAt").GetString());
        Assert.DoesNotContain("Private raw provider", failed.GetRawText());
        Assert.Equal(saved, await client.GetStringAsync($"/api/photographs/{id}/critique"));
        Assert.False(await RunAsync(factory));
        Assert.Equal(3, provider.Calls);
    }

    private static async Task<bool> RunAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCritiqueCommand());
    }

    private static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid id, long revision)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{id}/critique")
        { Content = JsonContent.Create(new { revision, regenerate = true }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }
}
