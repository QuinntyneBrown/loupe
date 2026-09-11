using Loupe.Application.Users;
using Loupe.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace Loupe.Infrastructure.Persistence;

public static class AccountSetup
{
    public static IServiceCollection AddLoupeAccounts(this IServiceCollection services)
    {
        services.AddOptions<DatabaseOptions>().BindConfiguration("ConnectionStrings")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Library), "ConnectionStrings:Library is required.").ValidateOnStart();
        services.AddDbContext<LibraryDbContext>((provider, options) => options.UseNpgsql(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.Library));
        services.AddScoped<IUserStore, UserStore>();
        services.AddSingleton<IPasswordService, PasswordService>();
        return services;
    }
}
