using Loupe.Application.ReferenceImports;
using Loupe.Infrastructure.Ai;
using Loupe.Infrastructure.ReferenceImports;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loupe.Worker;

public sealed class ReferenceImportWorker(IServiceScopeFactory scopes, IOptions<ReferenceImportOptions> options,
    IOptions<AiOptions> capacity, ILogger<ReferenceImportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.Mode != "Live") return;
        await Task.WhenAll(Enumerable.Range(0, capacity.Value.MaxConcurrentCalls).Select(_ => ProcessAsync(stoppingToken)));
    }

    private async Task ProcessAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                if (await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunReferenceImportCommand(), stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogError("Source import processing failed; inspect durable operation status"); }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
