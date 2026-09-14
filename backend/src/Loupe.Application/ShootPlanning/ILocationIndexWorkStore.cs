using Loupe.Domain.Operations;

namespace Loupe.Application.ShootPlanning;

public interface ILocationIndexWorkStore
{
    Task<BackgroundOperation?> ClaimAsync(CancellationToken cancellationToken);
    Task<bool> RenewAsync(BackgroundOperation operation, CancellationToken cancellationToken);
    /// <summary>The location's current document, or null when it no longer exists.</summary>
    Task<LocationIndexSource?> ReadAsync(BackgroundOperation operation, CancellationToken cancellationToken);
    /// <summary>Stores the vector and completes the operation only while the location still points at it at the same revision; otherwise the run is discarded.</summary>
    Task PublishAsync(BackgroundOperation operation, LocationIndexSource source, float[] vector, CancellationToken cancellationToken);
    /// <summary>Records a fresh intent for a location whose last index run failed.</summary>
    Task<Guid> RequeueAsync(Guid locationId, string ownerId, CancellationToken cancellationToken);
}
