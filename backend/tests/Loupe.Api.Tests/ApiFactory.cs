using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using Loupe.Api.Tests.Security;

namespace Loupe.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public ControlledIdentityProvider Identity { get; } = new();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        Identity.Dispose();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Identity:Authority"] = "https://identity.example",
            ["Identity:ClientId"] = "loupe-fixture",
            ["Identity:ClientSecret"] = "fixture-only-not-a-real-secret"
        }));
        builder.ConfigureTestServices(services => services.PostConfigure<OpenIdConnectOptions>("oidc", options =>
        {
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(Identity.Configuration);
            options.Backchannel = new HttpClient(Identity, disposeHandler: false);
        }));
    }
}
