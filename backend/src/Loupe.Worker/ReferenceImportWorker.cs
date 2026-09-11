using Loupe.Application.ReferenceImports;
using Loupe.Application.PhotographerImports;
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
        var photographerFirst = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                photographerFirst = !photographerFirst;
                var processed = photographerFirst
                    ? await sender.Send(new RunPhotographerImportCommand(), stoppingToken) || await sender.Send(new RunReferenceImportCommand(), stoppingToken)
                    : await sender.Send(new RunReferenceImportCommand(), stoppingToken) || await sender.Send(new RunPhotographerImportCommand(), stoppingToken);
                if (processed) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogError("Source import processing failed; inspect durable operation status"); }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
