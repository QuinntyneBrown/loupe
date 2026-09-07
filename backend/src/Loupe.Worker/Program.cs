using Loupe.Application.Maintenance;
using Loupe.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Loupe.Worker;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddLoupePersistence();
        builder.Services.AddMediatR(options =>
        {
            options.TypeEvaluator = type => type.Namespace == typeof(CleanDeletedContentCommand).Namespace;
            options.RegisterServicesFromAssemblyContaining<CleanDeletedContentCommand>();
        });
        builder.Services.AddOptions<CleanupOptions>().BindConfiguration("Cleanup")
            .Validate(options => options.PollInterval > TimeSpan.Zero && options.PollInterval <= TimeSpan.FromMinutes(5),
                "Cleanup:PollInterval must be positive and at most five minutes.").ValidateOnStart();
        builder.Services.AddHostedService<CleanupWorker>();
        using var host = builder.Build();
        await host.RunAsync();
    }
}
