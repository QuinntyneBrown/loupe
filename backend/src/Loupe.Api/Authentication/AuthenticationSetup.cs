using Loupe.Application.Sessions;
using Loupe.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
namespace Loupe.Api.Authentication;

public static class AuthenticationSetup
{
    public static IServiceCollection AddLoupeAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection("Jwt"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "Jwt:Issuer is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Jwt:Audience is required.")
            .Validate(o => o.HasValidKey, "Jwt:SigningKey must be base64 containing at least 32 random bytes.").ValidateOnStart();
        services.AddSingleton<SignInRateLimiter>();
        services.AddSingleton<IJwtService, JwtService>();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddScheme<SessionAuthenticationOptions, SessionAuthenticationHandler>(CookieAuthenticationDefaults.AuthenticationScheme, _ => { });
        return services;
    }
}
