using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Loupe.DemoIdentityProvider;

// Demo-only OIDC stub used solely to record product demo video locally.
// It is not a supported identity provider and must never be deployed.
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        // Persisted across restarts so a running Loupe.Api's cached JWKS (fetched via
        // OIDC discovery, refreshed only periodically) keeps matching this signing key.
        var keyPath = builder.Configuration["SigningKeyPath"]
            ?? Path.Combine(Path.GetTempPath(), "loupe-demo-idp-signing-key.pem");
        var signingKey = RSA.Create(2048);
        if (File.Exists(keyPath)) signingKey.ImportFromPem(File.ReadAllText(keyPath));
        else File.WriteAllText(keyPath, signingKey.ExportRSAPrivateKeyPem());
        var securityKey = new RsaSecurityKey(signingKey) { KeyId = "demo-key" };
        var pending = new ConcurrentDictionary<string, PendingAuthorization>();

        // A containerized Api reaches this provider through a different host name
        // (e.g. host.docker.internal) than the browser does (localhost). issuer,
        // token_endpoint and jwks_uri stay tied to whichever host made the discovery
        // request (kept self-consistent for that caller); authorization_endpoint is
        // always the browser-facing address, since only the browser is ever redirected there.
        var publicAuthorizeUrl = builder.Configuration["PublicAuthorizeUrl"] ?? "https://localhost:5444/authorize";

        app.MapGet("/.well-known/openid-configuration", (HttpContext context) =>
        {
            var issuer = Issuer(context);
            return Results.Json(new
            {
                issuer,
                authorization_endpoint = publicAuthorizeUrl,
                token_endpoint = $"{issuer}/token",
                jwks_uri = $"{issuer}/jwks",
                response_types_supported = new[] { "code" },
                subject_types_supported = new[] { "public" },
                id_token_signing_alg_values_supported = new[] { "RS256" },
                scopes_supported = new[] { "openid", "profile" },
                token_endpoint_auth_methods_supported = new[] { "none", "client_secret_post" },
                code_challenge_methods_supported = new[] { "S256" },
                claims_supported = new[] { "sub", "name" }
            });
        });

        app.MapGet("/jwks", () =>
        {
            var parameters = signingKey.ExportParameters(false);
            return Results.Json(new
            {
                keys = new[]
                {
                    new
                    {
                        kty = "RSA",
                        use = "sig",
                        alg = "RS256",
                        kid = securityKey.KeyId,
                        n = Base64UrlEncoder.Encode(parameters.Modulus),
                        e = Base64UrlEncoder.Encode(parameters.Exponent)
                    }
                }
            });
        });

        app.MapGet("/authorize", (HttpContext context) =>
        {
            var query = context.Request.Query;
            var subjectRaw = query["subject"].ToString();
            var subject = string.IsNullOrEmpty(subjectRaw) ? "demo-photographer" : subjectRaw;
            var redirectUri = query["redirect_uri"].ToString();
            var state = query["state"].ToString();
            var nonce = query["nonce"].ToString();
            var codeChallenge = query["code_challenge"].ToString();
            var responseMode = query["response_mode"].ToString();
            var html = $"""
                <!doctype html>
                <html><body style="font-family:sans-serif;max-width:420px;margin:80px auto;text-align:center">
                <h1>Loupe demo identity</h1>
                <p>Throwaway local identity provider used only to record demo video. Never a real login.</p>
                <form method="get" action="/authorize/confirm">
                  <input type="hidden" name="subject" value="{WebUtility.HtmlEncode(subject)}" />
                  <input type="hidden" name="redirect_uri" value="{WebUtility.HtmlEncode(redirectUri)}" />
                  <input type="hidden" name="state" value="{WebUtility.HtmlEncode(state)}" />
                  <input type="hidden" name="nonce" value="{WebUtility.HtmlEncode(nonce)}" />
                  <input type="hidden" name="code_challenge" value="{WebUtility.HtmlEncode(codeChallenge)}" />
                  <input type="hidden" name="response_mode" value="{WebUtility.HtmlEncode(responseMode)}" />
                  <button type="submit" style="font-size:1.1rem;padding:.75rem 1.5rem">Continue as {WebUtility.HtmlEncode(subject)}</button>
                </form>
                </body></html>
                """;
            return Results.Content(html, "text/html");
        });

        app.MapGet("/authorize/confirm", (HttpContext context) =>
        {
            var query = context.Request.Query;
            var subjectRaw = query["subject"].ToString();
            var subject = string.IsNullOrEmpty(subjectRaw) ? "demo-photographer" : subjectRaw;
            var redirectUri = query["redirect_uri"].ToString();
            var state = query["state"].ToString();
            var nonce = query["nonce"].ToString();
            var codeChallenge = query["code_challenge"].ToString();
            var responseMode = query["response_mode"].ToString();
            if (string.IsNullOrEmpty(redirectUri)) return Results.BadRequest(new { error = "invalid_request" });
            var code = Guid.NewGuid().ToString("N");
            pending[code] = new PendingAuthorization(subject, nonce, codeChallenge, DateTimeOffset.UtcNow.AddMinutes(5));
            if (responseMode == "form_post")
            {
                var formHtml = $"""
                    <!doctype html>
                    <html><body onload="document.forms[0].submit()">
                    <form method="post" action="{WebUtility.HtmlEncode(redirectUri)}">
                      <input type="hidden" name="code" value="{WebUtility.HtmlEncode(code)}" />
                      <input type="hidden" name="state" value="{WebUtility.HtmlEncode(state)}" />
                    </form>
                    </body></html>
                    """;
                return Results.Content(formHtml, "text/html");
            }
            var separator = redirectUri.Contains('?') ? "&" : "?";
            var location = $"{redirectUri}{separator}code={Uri.EscapeDataString(code)}&state={Uri.EscapeDataString(state)}";
            return Results.Redirect(location);
        });

        app.MapPost("/token", async (HttpContext context) =>
        {
            var form = await context.Request.ReadFormAsync();
            var code = form["code"].ToString();
            var verifier = form["code_verifier"].ToString();
            var clientId = form["client_id"].ToString();
            if (!pending.TryRemove(code, out var authorization) || authorization.ExpiresAt < DateTimeOffset.UtcNow)
                return Results.BadRequest(new { error = "invalid_grant" });
            if (!string.IsNullOrEmpty(authorization.CodeChallenge))
            {
                var expected = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
                if (expected != authorization.CodeChallenge)
                    return Results.BadRequest(new { error = "invalid_grant", error_description = "PKCE verification failed" });
            }
            var issuer = Issuer(context);
            var now = DateTime.UtcNow;
            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = issuer,
                Audience = clientId,
                IssuedAt = now,
                NotBefore = now,
                Expires = now.AddMinutes(5),
                Claims = new Dictionary<string, object>
                {
                    ["sub"] = authorization.Subject,
                    ["name"] = $"Demo photographer ({authorization.Subject})",
                    ["nonce"] = authorization.Nonce
                },
                SigningCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256)
            };
            var token = new JsonWebTokenHandler().CreateToken(descriptor);
            return Results.Json(new { access_token = "demo-only", token_type = "Bearer", expires_in = 300, id_token = token });
        });

        app.Run();
    }

    private static string Issuer(HttpContext context) => $"{context.Request.Scheme}://{context.Request.Host}";

    private sealed record PendingAuthorization(string Subject, string Nonce, string CodeChallenge, DateTimeOffset ExpiresAt);
}
