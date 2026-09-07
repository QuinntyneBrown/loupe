// Acceptance Test
// Traces to: L2-037
// Description: Begin a confidential OIDC code flow with PKCE and a protected return destination.
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace Loupe.Api.Tests.Security;

public sealed class SignInChallengeTests
{
    [Fact]
    public async Task L2_037_1_Challenge_uses_code_pkce_state_and_nonce()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        using var response = await client.GetAsync("/api/session/sign-in?returnUrl=%2Fmy-work");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location);
        Assert.Equal("https://identity.example/authorize", location.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.False(string.IsNullOrWhiteSpace(query["code_challenge"]));
        Assert.False(string.IsNullOrWhiteSpace(query["state"]));
        Assert.False(string.IsNullOrWhiteSpace(query["nonce"]));
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.Contains("secure", StringComparison.OrdinalIgnoreCase));
    }
}
