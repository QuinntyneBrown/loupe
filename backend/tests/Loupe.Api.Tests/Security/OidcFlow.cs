using Microsoft.AspNetCore.WebUtilities;

namespace Loupe.Api.Tests.Security;

public static class OidcFlow
{
    public static async Task<HttpResponseMessage> CompleteAsync(ApiFactory factory, HttpClient client, string subject = "owner-a")
    {
        using var challenge = await client.GetAsync("/api/session/sign-in?returnUrl=%2Fmy-work");
        var query = QueryHelpers.ParseQuery(challenge.Headers.Location!.Query);
        var code = factory.Identity.Authorize(query["nonce"].ToString(), subject);
        return await client.PostAsync("/signin-oidc", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["state"] = query["state"].ToString()
        }));
    }
}
