using Loupe.Domain.References;
using Loupe.Application.Common;

namespace Loupe.Application.References;

public interface IReferenceStore
{
    Task SaveAsync(Reference reference, CancellationToken cancellationToken);
    Task<Reference?> FindOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReferenceSummary>> ListAsync(string ownerId, int count, CreatedCursor? cursor, CancellationToken cancellationToken);
}
