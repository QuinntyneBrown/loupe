// Acceptance Test
// Traces to: L2-037, L2-038
// Description: JWT cryptographic validation is required even when a matching session record exists.
using System.Net;
using System.Text;
using Loupe.Application.Sessions;
using Loupe.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;
namespace Loupe.Api.Tests.Security;

public sealed class JwtValidationTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("signature")]
    [InlineData("unsigned")]
    [InlineData("algorithm")]
    [InlineData("missing-claims")]
    [InlineData("wrong-subject")]
    [InlineData("malformed")]
    public async Task L2_037_2_Invalid_JWT_never_authenticates_a_session(string fault)
    {
        await using var factory = new ApiFactory(database.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var login = await LocalSignInFlow.CompleteAsync(factory, client);
        login.EnsureSuccessStatusCode();
        var original = Assert.Single(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith("__Host-loupe-session=")).Split(';')[0].Split('=', 2)[1];
        var now = factory.Clock.GetUtcNow();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(fault == "signature" ? "another-fixture-local-signing-key-32-bytes" : "fixture-only-local-signing-key-32-bytes".PadRight(64, 'x')));
        var token = fault == "malformed" ? "not.a.jwt" : new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = fault == "issuer" ? "Untrusted" : "Loupe",
            Audience = fault == "audience" ? "Other" : "Loupe",
            IssuedAt = now.AddMinutes(-2).UtcDateTime,
            NotBefore = now.AddMinutes(-2).UtcDateTime,
            Expires = (fault == "expired" ? now.AddSeconds(-1) : now.AddHours(1)).UtcDateTime,
            Claims = fault == "missing-claims" ? [] : new Dictionary<string, object> { ["sub"] = fault == "wrong-subject" ? "owner-b" : "owner-a", ["jti"] = Guid.NewGuid().ToString("N") },
            SigningCredentials = fault == "unsigned" ? null : new SigningCredentials(key, fault == "algorithm" ? SecurityAlgorithms.HmacSha384 : SecurityAlgorithms.HmacSha256)
        });
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<LibraryDbContext>().Sessions.Where(s => s.Id == SessionToken.Hash(original))
                .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Id, SessionToken.Hash(token)!));
        using var replay = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        replay.DefaultRequestHeaders.Add("Cookie", "__Host-loupe-session=" + token);
        using var rejected = await replay.GetAsync("/api/session");
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
    }
}
