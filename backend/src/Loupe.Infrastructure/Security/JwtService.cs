using Loupe.Application.Sessions;
using Loupe.Domain.Sessions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
namespace Loupe.Infrastructure.Security;

public sealed class JwtService(IOptions<JwtOptions> options, TimeProvider clock) : IJwtService
{
    private readonly JsonWebTokenHandler handler = new() { MapInboundClaims = false };
    public string Issuer => options.Value.Issuer;
    private SymmetricSecurityKey Key => new(Convert.FromBase64String(options.Value.SigningKey));
    public string Create(ApplicationSession session) => handler.CreateToken(new SecurityTokenDescriptor
    {
        Issuer = Issuer,
        Audience = options.Value.Audience,
        IssuedAt = session.CreatedAt.UtcDateTime,
        NotBefore = session.CreatedAt.UtcDateTime,
        // JWT timestamps have whole-second precision; database expiry enforces the exact boundary.
        Expires = DateTimeOffset.FromUnixTimeSeconds(session.CreatedAt.AddHours(12).ToUnixTimeSeconds() + 1).UtcDateTime,
        Claims = new Dictionary<string, object> { ["sub"] = session.Subject, ["jti"] = Guid.NewGuid().ToString("N") },
        SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256)
    });
    public async Task<string?> ValidateAsync(string token)
    {
        var result = await handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = options.Value.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = Key,
            RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            LifetimeValidator = (notBefore, expires, _, _) => expires.HasValue && notBefore.HasValue &&
                notBefore.Value <= clock.GetUtcNow().UtcDateTime && expires.Value > clock.GetUtcNow().UtcDateTime
        });
        if (!result.IsValid || string.IsNullOrWhiteSpace(result.ClaimsIdentity.FindFirst("jti")?.Value)) return null;
        return result.ClaimsIdentity.FindFirst("sub")?.Value;
    }
}
