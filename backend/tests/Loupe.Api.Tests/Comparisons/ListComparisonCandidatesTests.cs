// Given a private library, when comparison candidates are paged, then successful
// saved attempts are filtered before pagination and no new analysis is requested.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Critiques;
using Loupe.Api.Tests.Photographs;
using Loupe.Application.Critiques;
using Loupe.Application.Operations;
using Loupe.Domain.Critiques;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.Comparisons;

public sealed class ListComparisonCandidatesTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_005_1_2_Eligible_filter_precedes_pagination_preserves_order_and_excludes_deleted_and_foreign_items()
    {
        var fail = false;
        var provider = new ControlledCritiqueProvider((_, _, _) => fail
            ? Task.FromException<CritiqueResult>(new ProviderFailureException(ProviderFailureKind.InvalidCredentials))
            : Task.FromResult(CritiqueResultFixture.Valid()));
        await using var factory = CreateFactory(provider);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var ids = new List<Guid>();
        for (var index = 0; index < 25; index++)
        {
            await PhotographFixture.UploadAsync(owner, "Not yet critiqued");
            ids.Add(await CreateAttemptAsync(factory, owner, $"Attempt {index}"));
            factory.Clock.Advance(TimeSpan.FromSeconds(1));
        }
        var foreign = await CreateAttemptAsync(factory, stranger, "Foreign attempt");
        fail = true;
        await SubmitAsync(owner, ids[0], 2, true);
        await RunAsync(factory);
        var calls = provider.Calls;
        using var response = await owner.GetAsync("/api/comparisons/eligible");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var first = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(24, first.GetProperty("items").GetArrayLength());
        var cursor = first.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrEmpty(cursor));
        var second = await owner.GetFromJsonAsync<JsonElement>("/api/comparisons/eligible?cursor=" + Uri.EscapeDataString(cursor!));
        Assert.Single(second.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, second.GetProperty("nextCursor").ValueKind);
        var items = first.GetProperty("items").EnumerateArray().Concat(second.GetProperty("items").EnumerateArray()).ToArray();
        Assert.Equal(ids.AsEnumerable().Reverse(), items.Select(item => item.GetProperty("id").GetGuid()));
        Assert.All(items, item => Assert.True(item.GetProperty("hasCritique").GetBoolean()));
        Assert.DoesNotContain(items, item => item.GetProperty("id").GetGuid() == foreign);
        Assert.Equal("Failed", items[^1].GetProperty("critiqueStatus").GetString());
        Assert.Equal(calls, provider.Calls);
        using var deleted = await owner.DeleteAsync($"/api/photographs/{ids[0]}?revision=2");
        deleted.EnsureSuccessStatusCode();
        var after = await owner.GetFromJsonAsync<JsonElement>("/api/comparisons/eligible?pageSize=100");
        Assert.Equal(24, after.GetProperty("items").GetArrayLength());
        Assert.DoesNotContain(after.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == ids[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task L2_005_2_Fewer_than_two_eligible_attempts_are_reported_without_uncritiqued_placeholders(int count)
    {
        await using var factory = CreateFactory();
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        await PhotographFixture.UploadAsync(owner);
        for (var index = 0; index < count; index++) await CreateAttemptAsync(factory, owner, "Attempt");
        using var response = await owner.GetAsync("/api/comparisons/eligible");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(count, page.GetProperty("items").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, page.GetProperty("nextCursor").ValueKind);
    }

    [Theory]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("cursor=invalid", "cursor")]
    public async Task L2_048_2_Candidate_pagination_rejects_invalid_bounds_and_cursors(string query, string field)
    {
        await using var factory = CreateFactory();
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var response = await owner.GetAsync("/api/comparisons/eligible?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }

    private ApiFactory CreateFactory(ICritiqueProvider? provider = null) => new(database.ConnectionString, database.MediaRoot)
    { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Live", ["Ai:ApiKey"] = "fixture-only-key" }, CritiqueProvider = provider };

    private static async Task<Guid> CreateAttemptAsync(ApiFactory factory, HttpClient owner, string title)
    {
        var id = (await PhotographFixture.UploadAsync(owner, title)).GetProperty("id").GetGuid();
        await SubmitAsync(owner, id, 1);
        await RunAsync(factory);
        return id;
    }

    private static async Task SubmitAsync(HttpClient owner, Guid id, long revision, bool regenerate = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{id}/critique")
        { Content = JsonContent.Create(new { revision, regenerate }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await owner.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static async Task RunAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCritiqueCommand()));
    }
}
