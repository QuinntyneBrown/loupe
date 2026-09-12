using Loupe.Domain.References;
using Loupe.Application.Common;

namespace Loupe.Application.References;

public interface IReferenceStore
{
    Task SaveAsync(Reference reference, CancellationToken cancellationToken);
    Task<Reference> SaveSourceAsync(Reference reference, CancellationToken cancellationToken);
    Task<Reference> UpdateAsync(Guid id, string ownerId, long revision, ReferenceMetadata metadata, CancellationToken cancellationToken);
    Task<Reference?> FindOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken);
    Task<Reference?> FindSourceOwnedAsync(string ownerId, string source, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReferenceSummary>> ListAsync(string ownerId, int count, CreatedCursor? cursor, Guid? boardId, string[] tags, CancellationToken cancellationToken);
    Task<int> CountAsync(string ownerId, Guid? boardId, string[] tags, CancellationToken cancellationToken);
}
