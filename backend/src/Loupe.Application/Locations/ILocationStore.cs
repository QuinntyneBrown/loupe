using Loupe.Domain.Locations;

namespace Loupe.Application.Locations;

public interface ILocationStore
{
    Task SaveAsync(Location location, CancellationToken cancellationToken);
    Task<Location?> FindOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken);
    Task<Location> UpdateAsync(Guid id, string ownerId, long revision, LocationDetails details, CancellationToken cancellationToken);
    Task<Location> UpdateTextAsync(Guid id, string ownerId, long revision, LocationTextField field, string? text, CancellationToken cancellationToken);
    Task<Location> ReplaceTagsAsync(Guid id, string ownerId, long revision, IReadOnlyList<LocationTagInput> tags, CancellationToken cancellationToken);
}
