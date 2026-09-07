using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace Loupe.Api.Authentication;

public static class AuthenticationSetup
{
    public static IServiceCollection AddLoupeAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IdentityOptions>().Bind(configuration.GetSection("Identity"))
            .Validate(options => Uri.TryCreate(options.Authority, UriKind.Absolute, out var uri) && uri.Scheme == "https",
                "Identity:Authority must be an absolute HTTPS URL.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ClientId), "Identity:ClientId is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ClientSecret), "Identity:ClientSecret is required.")
            .ValidateOnStart();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddScheme<SessionAuthenticationOptions, SessionAuthenticationHandler>(CookieAuthenticationDefaults.AuthenticationScheme,
                options => options.ClaimsIssuer = "Loupe")
            .AddOpenIdConnect("oidc", options => options.ResponseType = "code");
        services.AddOptions<OpenIdConnectOptions>("oidc").Configure<IOptions<IdentityOptions>>((options, settings) =>
        {
            options.Authority = settings.Value.Authority;
            options.ClientId = settings.Value.ClientId;
            options.ClientSecret = settings.Value.ClientSecret;
            options.UsePkce = true;
            options.SaveTokens = false;
            options.MapInboundClaims = false;
            options.GetClaimsFromUserInfoEndpoint = false;
            options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.Events.OnRemoteFailure = context =>
            {
                context.HandleResponse();
                context.Response.Redirect("/sign-in?error=authentication_failed");
                return Task.CompletedTask;
            };
        });
        return services;
    }
}
