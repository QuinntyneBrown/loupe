using Loupe.Application.Maintenance;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loupe.Worker;

public sealed class CleanupWorker(IServiceScopeFactory scopes, IOptions<CleanupOptions> options,
    TimeProvider clock, ILogger<CleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CleanDeletedContentCommand(), stoppingToken);
                await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CleanAbandonedMediaCommand(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogError("Cleanup iteration failed; durable work remains pending"); }
            await Task.Delay(options.Value.PollInterval, clock, stoppingToken);
        }
    }
}
