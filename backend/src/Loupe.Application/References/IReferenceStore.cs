using Loupe.Domain.References;
using Loupe.Application.Common;

namespace Loupe.Application.References;

public interface IReferenceStore
{
    Task SaveAsync(Reference reference, CancellationToken cancellationToken);
    Task<Reference> UpdateAsync(Guid id, string ownerId, long revision, ReferenceMetadata metadata, CancellationToken cancellationToken);
    Task<Reference?> FindOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReferenceSummary>> ListAsync(string ownerId, int count, CreatedCursor? cursor, CancellationToken cancellationToken);
}
