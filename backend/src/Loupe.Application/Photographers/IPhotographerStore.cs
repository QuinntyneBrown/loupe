using Loupe.Domain.Photographers;
using Loupe.Application.Common;

namespace Loupe.Application.Photographers;

public interface IPhotographerStore
{
    Task<Photographer> SaveAsync(Photographer photographer, CancellationToken cancellationToken);
    Task<Photographer?> FindOwnedAsync(string ownerId, Guid id, CancellationToken cancellationToken);
    Task<Photographer?> FindSourceOwnedAsync(string ownerId, string url, CancellationToken cancellationToken);
    Task<Photographer> UpdateAsync(string ownerId, Guid id, long revision, PhotographerMetadata metadata, CancellationToken cancellationToken);
    Task<IReadOnlyList<PhotographerSummary>> ListAsync(string ownerId, int count, CreatedCursor? cursor, CancellationToken cancellationToken);
    Task<int> CountAsync(string ownerId, CancellationToken cancellationToken);
}
