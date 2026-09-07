// Acceptance Test
// Traces to: L2-037
// Description: Actual signed OIDC callbacks establish sessions only with valid protocol checks.
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace Loupe.Api.Tests.Security;

public sealed class IdentityCallbackTests
{
    [Theory]
    [InlineData("nonce")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("signature")]
    [InlineData("state")]
    public async Task L2_037_2_Invalid_callback_returns_safe_retry_guidance_without_session(string fault)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        using var challenge = await client.GetAsync("/api/session/sign-in?returnUrl=%2Fmy-work");
        var query = QueryHelpers.ParseQuery(challenge.Headers.Location!.Query);
        var code = factory.Identity.Authorize(query["nonce"].ToString(), fault: fault);
        using var callback = await client.PostAsync("/signin-oidc", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["state"] = fault == "state" ? "tampered" : query["state"].ToString()
        }));
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/sign-in?error=authentication_failed", callback.Headers.Location?.OriginalString);
        using var session = await client.GetAsync("/api/session");
        Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
    }
}
