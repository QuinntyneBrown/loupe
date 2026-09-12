using Loupe.Application.Common;

namespace Loupe.Application.ShootPlanning;

public interface ILocationSearchStore
{
    Task<IReadOnlyList<LocationSearchItem>> ListAsync(string ownerId, LocationSearchFilter filter, int count, CreatedCursor? cursor, CancellationToken cancellationToken);
    Task<int> CountAsync(string ownerId, LocationSearchFilter filter, CancellationToken cancellationToken);
}
