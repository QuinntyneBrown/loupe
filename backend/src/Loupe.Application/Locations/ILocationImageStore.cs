using Loupe.Application.Images;
using Loupe.Domain.Locations;

namespace Loupe.Application.Locations;

public interface ILocationImageStore
{
    Task<int> CountAsync(Guid locationId, string ownerId, CancellationToken cancellationToken);
    Task AddAsync(Guid locationId, string ownerId, ProcessedImage image, string imageKey, string previewKey, CancellationToken cancellationToken);
    Task<Location> RemoveAsync(Guid locationId, string ownerId, Guid imageId, long revision, CancellationToken cancellationToken);
    Task<Location> SetCoverAsync(Guid locationId, string ownerId, Guid imageId, long revision, CancellationToken cancellationToken);
}
