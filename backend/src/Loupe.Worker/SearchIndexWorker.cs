using Loupe.Application.ShootPlanning;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Loupe.Worker;

/// <summary>Keeps location search vectors current; runs whatever the AI mode, and idles when no embedding endpoint is configured.</summary>
public sealed class SearchIndexWorker(IServiceScopeFactory scopes, ILogger<SearchIndexWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                if (await sender.Send(new RefreshLocationSearchDocumentCommand(), stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogError("Search index processing failed; inspect durable operation status"); }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
