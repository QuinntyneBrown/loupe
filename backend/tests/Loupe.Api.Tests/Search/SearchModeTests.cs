using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Loupe.Api.Tests.Search;

public sealed class SearchModeTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData("meaning")]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData("%20")]
    public async Task Given_an_unsupported_mode_when_searching_then_return_a_mode_field_error(string mode)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var response = await owner.GetAsync("/api/search?mode=" + mode);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEmpty(problem.GetProperty("errors").GetProperty("mode").EnumerateArray());
    }

    [Fact]
    public async Task Given_keyword_mode_when_explicit_or_omitted_then_return_the_same_library()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var owner = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/photographers")
        {
            Content = JsonContent.Create(new { name = "Keyword mode", portfolioUrl = "https://keyword.example/" })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var saved = await owner.SendAsync(request);
        saved.EnsureSuccessStatusCode();
        using var omitted = await owner.GetAsync("/api/search?query=keyword");
        using var explicitMode = await owner.GetAsync("/api/search?query=keyword&mode=keyword");
        Assert.Equal(HttpStatusCode.OK, omitted.StatusCode);
        Assert.Equal(HttpStatusCode.OK, explicitMode.StatusCode);
        Assert.Equal(await omitted.Content.ReadAsStringAsync(), await explicitMode.Content.ReadAsStringAsync());
        Assert.Equal(1, (await explicitMode.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalCount").GetInt32());
    }
}
