// Given two distinct owned photographs with successful critiques, when compared,
// then existing content is returned without creating new analysis work.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Loupe.Api.Tests.Photographs;
using Loupe.Application.Critiques;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Loupe.Api.Tests.Comparisons;

public sealed class GetComparisonTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_005_1_Comparison_returns_each_saved_attempt_and_does_not_admit_analysis()
    {
        await using var factory = CreateFactory();
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var first = await CreateAttemptAsync(factory, owner, "First attempt");
        var second = await CreateAttemptAsync(factory, owner, "Second attempt");
        using var notes = await owner.PutAsJsonAsync($"/api/photographs/{first}/notes", new { revision = 2, notes = "Private practice notes" });
        notes.EnsureSuccessStatusCode();
        using var brief = await owner.PutAsJsonAsync($"/api/photographs/{first}/brief", new { revision = 3, intent = "A later intent" });
        brief.EnsureSuccessStatusCode();
        var beforeFirst = await owner.GetStringAsync($"/api/photographs/{first}/critique/operation");
        var beforeSecond = await owner.GetStringAsync($"/api/photographs/{second}/critique/operation");
        using var response = await owner.GetAsync(Location(first, second));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var (side, id) in new[] { ("first", first), ("second", second) })
        {
            var attempt = result.GetProperty(side);
            Assert.Equal(await owner.GetStringAsync($"/api/photographs/{id}"), attempt.GetProperty("photograph").GetRawText());
            Assert.Equal(await owner.GetStringAsync($"/api/photographs/{id}/critique"), attempt.GetProperty("critique").GetRawText());
        }
        Assert.Equal("Private practice notes", result.GetProperty("first").GetProperty("photograph").GetProperty("notes").GetString());
        Assert.Equal("A later intent", result.GetProperty("first").GetProperty("photograph").GetProperty("brief").GetProperty("intent").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("first").GetProperty("critique").GetProperty("brief").GetProperty("intent").ValueKind);
        Assert.Equal(beforeFirst, await owner.GetStringAsync($"/api/photographs/{first}/critique/operation"));
        Assert.Equal(beforeSecond, await owner.GetStringAsync($"/api/photographs/{second}/critique/operation"));
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCritiqueCommand()));
    }

    [Theory]
    [InlineData("same", "secondId")]
    [InlineData("missing-critique", "secondId")]
    [InlineData("empty", "firstId")]
    public async Task L2_005_3_Invalid_selection_has_a_field_error(string kind, string field)
    {
        await using var factory = CreateFactory();
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var first = await CreateAttemptAsync(factory, owner, "First");
        var second = kind == "same" ? first : (await PhotographFixture.UploadAsync(owner)).GetProperty("id").GetGuid();
        using var response = await owner.GetAsync(Location(kind == "empty" ? Guid.Empty : first, second));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_request", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task L2_005_3_Owner_visibility_is_checked_before_critique_eligibility()
    {
        await using var factory = CreateFactory();
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var stranger = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var withoutCritique = (await PhotographFixture.UploadAsync(owner)).GetProperty("id").GetGuid();
        var foreign = await CreateAttemptAsync(factory, stranger, "Foreign attempt");
        foreach (var id in new[] { foreign, Guid.NewGuid() })
        {
            using var response = await owner.GetAsync(Location(withoutCritique, id));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("item_unavailable", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task L2_005_4_Deleted_attempt_revokes_comparison_but_the_survivor_remains_available()
    {
        await using var factory = CreateFactory();
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var first = await CreateAttemptAsync(factory, owner, "First");
        var second = await CreateAttemptAsync(factory, owner, "Second");
        using var before = await owner.GetAsync(Location(first, second));
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        using var deleted = await owner.DeleteAsync($"/api/photographs/{first}?revision=2");
        deleted.EnsureSuccessStatusCode();
        using var after = await owner.GetAsync(Location(first, second));
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
        using var survivor = await owner.GetAsync($"/api/photographs/{second}/critique");
        Assert.Equal(HttpStatusCode.OK, survivor.StatusCode);
    }

    [Fact]
    public async Task L2_005_3_Comparison_requires_authentication()
    {
        await using var factory = CreateFactory();
        using var anonymous = factory.CreateClient();
        using var response = await anonymous.GetAsync(Location(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private ApiFactory CreateFactory() => new(database.ConnectionString, database.MediaRoot)
    { Settings = new Dictionary<string, string?> { ["Ai:Mode"] = "Demo" } };

    private static string Location(Guid first, Guid second) => $"/api/comparisons?firstId={first}&secondId={second}";

    private static async Task<Guid> CreateAttemptAsync(ApiFactory factory, HttpClient owner, string title)
    {
        var id = (await PhotographFixture.UploadAsync(owner, title)).GetProperty("id").GetGuid();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/photographs/{id}/critique")
        { Content = JsonContent.Create(new { revision = 1, regenerate = false }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await owner.SendAsync(request);
        response.EnsureSuccessStatusCode();
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCritiqueCommand()));
        return id;
    }
}
