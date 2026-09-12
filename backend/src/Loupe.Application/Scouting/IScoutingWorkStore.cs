using Loupe.Domain.Operations;
using Loupe.Domain.Scouting;

namespace Loupe.Application.Scouting;

public interface IScoutingWorkStore
{
    Task<BackgroundOperation?> ClaimAsync(ExecutionMode mode, CancellationToken cancellationToken);
    Task<bool> RenewAsync(BackgroundOperation operation, CancellationToken cancellationToken);
    Task PublishAsync(BackgroundOperation operation, ScoutingReport report, CancellationToken cancellationToken);
}
