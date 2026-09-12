using Loupe.Domain.Locations;

namespace Loupe.Application.Locations;

public interface ILocationStore
{
    Task SaveAsync(Location location, CancellationToken cancellationToken);
    Task<Location?> FindOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken);
}
