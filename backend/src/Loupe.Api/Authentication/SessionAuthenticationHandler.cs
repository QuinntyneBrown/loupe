using System.Security.Claims;
using System.Text.Encodings.Web;
using Loupe.Application.Sessions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Loupe.Api.Authentication;

public sealed class SessionAuthenticationHandler(IOptionsMonitor<SessionAuthenticationOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, ISender sender)
    : SignInAuthenticationHandler<SessionAuthenticationOptions>(options, logger, encoder)
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

    protected override async Task HandleSignInAsync(ClaimsPrincipal user, AuthenticationProperties? properties)
    {
        var subject = user.FindFirst("sub") ?? throw new AuthenticationFailureException("Missing subject.");
        var token = await sender.Send(new CompleteSignInCommand(subject.Issuer, subject.Value,
            user.FindFirst("name")?.Value ?? "Photographer", Request.Cookies[Options.CookieName]), Context.RequestAborted);
        Response.Cookies.Append(Options.CookieName, token, CookieOptions());
    }

    protected override async Task HandleSignOutAsync(AuthenticationProperties? properties)
    {
        await sender.Send(new RevokeSessionCommand(Request.Cookies[Options.CookieName]), Context.RequestAborted);
        Response.Cookies.Delete(Options.CookieName, CookieOptions());
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    private static CookieOptions CookieOptions() => new()
    {
        Path = "/",
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        IsEssential = true
    };
}
