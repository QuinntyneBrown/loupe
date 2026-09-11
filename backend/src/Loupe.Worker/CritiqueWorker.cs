using Loupe.Application.Critiques;
using Loupe.Infrastructure.Ai;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loupe.Worker;

public sealed class CritiqueWorker(IServiceScopeFactory scopes, IOptions<AiOptions> options, ILogger<CritiqueWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.Mode != "Live") return;
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
        {
            logger.LogWarning("Live critique processing requires configured analysis credentials");
            return;
        }
        await Task.WhenAll(Enumerable.Range(0, options.Value.MaxConcurrentCalls).Select(_ => ProcessAsync(stoppingToken)));
    }

    private async Task ProcessAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                if (await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCritiqueCommand(), stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogError("Critique processing failed; inspect durable operation status"); }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
