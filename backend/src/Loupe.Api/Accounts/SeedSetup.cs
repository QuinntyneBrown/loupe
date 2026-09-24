namespace Loupe.Api.Accounts;

public static class SeedSetup
{
    public static IServiceCollection AddLoupeSeedAccount(this IServiceCollection services)
    {
        services.AddOptions<SeedOptions>().BindConfiguration("Seed")
            .Validate(o => !o.IsConfigured || o.HasValidEmail, "Seed:Email must be a valid email address when a seed account is configured.")
            .Validate(o => !o.IsConfigured || o.HasValidName, "Seed:Name must contain 1 to 200 characters when a seed account is configured.")
            .Validate(o => !o.IsConfigured || o.HasValidPassword, "Seed:Password must contain 8 to 128 characters when a seed account is configured.")
            .ValidateOnStart();
        services.AddHostedService<SeedAccountService>();
        return services;
    }
}
