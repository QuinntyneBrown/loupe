using System.Security.Claims;
using System.Text.Encodings.Web;
using Loupe.Application.Sessions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Loupe.Api.Authentication;

public sealed class SessionAuthenticationHandler(IOptionsMonitor<SessionAuthenticationOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, ISender sender)
    : AuthenticationHandler<SessionAuthenticationOptions>(options, logger, encoder), IAuthenticationSignOutHandler
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var session = await sender.Send(new AuthenticateSessionQuery(Request.Cookies[Options.CookieName]), Context.RequestAborted);
        if (session is null) return AuthenticateResult.NoResult();
        var identity = new ClaimsIdentity([
            new Claim("iss", session.Issuer), new Claim("sub", session.Subject), new Claim("name", session.Name)
        ], Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    public async Task SignOutAsync(AuthenticationProperties? properties)
    {
        await sender.Send(new RevokeSessionCommand(Request.Cookies[Options.CookieName]), Context.RequestAborted);
        Response.Cookies.Delete(Options.CookieName, CookieOptions());
        Response.StatusCode = StatusCodes.Status204NoContent;
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    internal static CookieOptions CookieOptions() => new()
    {
        Path = "/",
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        IsEssential = true
    };
}
