using Loupe.Application.Videos;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loupe.Worker;

public sealed class VideoIndexWorker(IServiceScopeFactory scopes, IOptions<IndexingOptions> options, TimeProvider clock, ILogger<VideoIndexWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var run = logger.BeginScope(new Dictionary<string, object> { ["EntryPoint"] = "video_index_worker", ["RunId"] = Guid.NewGuid().ToString("N") });
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new IndexVideosCommand(), stoppingToken);
                if (result.Failure is { } failure) logger.LogWarning("Video embedding failed with {Kind}; unindexed videos are retried next iteration", failure);
                else if (result.Indexed > 0) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogError("Video indexing iteration failed; unindexed videos remain pending"); }
            await Task.Delay(options.Value.PollInterval, clock, stoppingToken);
        }
    }
}
