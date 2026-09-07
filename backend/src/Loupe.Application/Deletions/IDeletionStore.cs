using Loupe.Domain.Deletions;

namespace Loupe.Application.Deletions;

public interface IDeletionStore
{
    Task<DeletionOperation> DeletePhotographAsync(Guid id, string ownerId, long revision, CancellationToken cancellationToken);
    Task<DeletionOperation?> FindOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken);
}
