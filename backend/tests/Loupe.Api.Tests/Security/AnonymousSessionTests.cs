// Acceptance Test
// Traces to: L2-037, L2-038, L2-042
// Description: Private session reads reject unauthenticated requests without cacheable content.
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Loupe.Api.Tests.Security;

public sealed class AnonymousSessionTests
{
    [Fact]
    public async Task L2_037_1_And_038_5_Anonymous_session_read_returns_private_401()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        using var response = await client.GetAsync("/api/session");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.Null(response.Headers.Location);
        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("authentication_required", problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("correlationId").GetString()));
    }
}
