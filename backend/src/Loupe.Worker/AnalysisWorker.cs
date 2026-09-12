using Loupe.Application.Critiques;
using Loupe.Application.ReferenceAnalysis;
using Loupe.Application.PhotographerSummaries;
using Loupe.Application.Scouting;
using Loupe.Infrastructure.Ai;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loupe.Worker;

public sealed class AnalysisWorker(IServiceScopeFactory scopes, IOptions<AiOptions> options, ILogger<AnalysisWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.Mode != "Live") return;
        if (!options.Value.IsConfigured)
        {
            logger.LogWarning("Live image analysis requires Ai:Endpoint, Ai:Deployment, and Ai:ApiKey configuration");
            return;
        }
        await Task.WhenAll(Enumerable.Range(0, options.Value.MaxConcurrentCalls).Select(_ => ProcessAsync(stoppingToken)));
    }

    private async Task ProcessAsync(CancellationToken stoppingToken)
    {
        var next = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var processed = false;
                for (var offset = 0; offset < 4 && !processed; offset++)
                {
                    var kind = next;
                    next = (next + 1) % 4;
                    processed = kind switch
                    {
                        0 => await sender.Send(new RunReferenceAnalysisCommand(), stoppingToken),
                        1 => await sender.Send(new RunCritiqueCommand(), stoppingToken),
                        2 => await sender.Send(new RunPhotographerSummaryCommand(), stoppingToken),
                        _ => await sender.Send(new RunScoutingReportCommand(), stoppingToken)
                    };
                }
                if (processed) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogError("Image analysis failed; inspect durable operation status"); }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
