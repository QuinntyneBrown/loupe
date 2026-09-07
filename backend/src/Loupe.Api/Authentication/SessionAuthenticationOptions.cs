using Microsoft.AspNetCore.Authentication;

namespace Loupe.Api.Authentication;

public sealed class SessionAuthenticationOptions : AuthenticationSchemeOptions
{
    public string CookieName { get; set; } = "__Host-loupe-session";
}
