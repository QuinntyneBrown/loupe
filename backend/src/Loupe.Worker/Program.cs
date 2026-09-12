using Loupe.Application.Maintenance;
using Loupe.Application.Critiques;
using Loupe.Application.ReferenceAnalysis;
using Loupe.Application.ReferenceImports;
using Loupe.Application.PhotographerImports;
using Loupe.Application.PhotographerSummaries;
using Loupe.Application.Scouting;
using Loupe.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Loupe.Worker;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders().AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.UseUtcTimestamp = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fff'Z'";
        });
        builder.Services.AddLoupePersistence();
        builder.Services.AddMediatR(options =>
        {
            options.TypeEvaluator = type => type.Namespace == typeof(CleanDeletedContentCommand).Namespace
                || type == typeof(RunCritiqueCommandHandler) || type == typeof(RunReferenceImportCommandHandler)
                || type == typeof(RunReferenceAnalysisCommandHandler) || type == typeof(RunPhotographerImportCommandHandler)
                || type == typeof(RunPhotographerSummaryCommandHandler) || type == typeof(RunScoutingReportCommandHandler);
            options.RegisterServicesFromAssemblyContaining<CleanDeletedContentCommand>();
        });
        builder.Services.AddOptions<CleanupOptions>().BindConfiguration("Cleanup")
            .Validate(options => options.PollInterval > TimeSpan.Zero && options.PollInterval <= TimeSpan.FromMinutes(5),
                "Cleanup:PollInterval must be positive and at most five minutes.").ValidateOnStart();
        builder.Services.AddHostedService<CleanupWorker>();
        builder.Services.AddHostedService<AnalysisWorker>();
        builder.Services.AddHostedService<ReferenceImportWorker>();
        using var host = builder.Build();
        using var scope = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Loupe.Worker.Host")
            .BeginScope(new Dictionary<string, object> { ["EntryPoint"] = "worker_host", ["RunId"] = Guid.NewGuid().ToString("N") });
        await host.RunAsync();
    }
}
