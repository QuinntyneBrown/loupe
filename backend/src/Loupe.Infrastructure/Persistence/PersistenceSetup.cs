using Loupe.Application.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Loupe.Infrastructure.Persistence;

public static class PersistenceSetup
{
    public static IServiceCollection AddLoupePersistence(this IServiceCollection services)
    {
        services.AddOptions<DatabaseOptions>().BindConfiguration("ConnectionStrings")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Library), "ConnectionStrings:Library is required.").ValidateOnStart();
        services.AddDbContext<LibraryDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.Library));
        services.AddScoped<ISessionStore, SessionStore>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
