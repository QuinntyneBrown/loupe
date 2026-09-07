using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Loupe.Api.Tests.Security;

public sealed class ControlledIdentityProvider : HttpMessageHandler
{
    private readonly RSA signingKey = RSA.Create(2048);
    private readonly ConcurrentDictionary<string, (string Nonce, string Subject, string? Fault)> codes = new();
    public OpenIdConnectConfiguration Configuration
    {
        get
        {
            var configuration = new OpenIdConnectConfiguration
            {
                Issuer = "https://identity.example",
                AuthorizationEndpoint = "https://identity.example/authorize",
                TokenEndpoint = "https://identity.example/token"
            };
            configuration.SigningKeys.Add(new RsaSecurityKey(signingKey) { KeyId = "fixture-key" });
            return configuration;
        }
    }

    public string Authorize(string nonce, string subject = "owner-a", string? fault = null)
    {
        var code = Guid.NewGuid().ToString("N");
        codes[code] = (nonce, subject, fault);
        return code;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri?.AbsoluteUri != "https://identity.example/token")
            throw new InvalidOperationException("The identity fixture cannot make external requests.");
        var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken));
        if (!codes.TryRemove(form["code"].ToString(), out var identity)) return new HttpResponseMessage(HttpStatusCode.BadRequest);
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = identity.Fault == "issuer" ? "https://untrusted.example" : "https://identity.example",
            Audience = identity.Fault == "audience" ? "another-client" : "loupe-fixture",
            IssuedAt = now.AddMinutes(-10),
            NotBefore = now.AddMinutes(-10),
            Expires = identity.Fault == "expired" ? now.AddMinutes(-6) : now.AddMinutes(5),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = identity.Subject,
                ["name"] = "Fixture photographer",
                ["nonce"] = identity.Nonce + (identity.Fault == "nonce" ? "-wrong" : "")
            },
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(signingKey) { KeyId = "fixture-key" }, SecurityAlgorithms.RsaSha256)
        };
        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        if (identity.Fault == "signature") token = token[..(token.LastIndexOf('.') + 1)] + Base64UrlEncoder.Encode(new byte[256]);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { access_token = "fixture-only", token_type = "Bearer", expires_in = 300, id_token = token })
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) signingKey.Dispose();
        base.Dispose(disposing);
    }
}
