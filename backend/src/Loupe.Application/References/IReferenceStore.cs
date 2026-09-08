using Loupe.Domain.References;

namespace Loupe.Application.References;

public interface IReferenceStore
{
    Task SaveAsync(Reference reference, CancellationToken cancellationToken);
    Task<Reference?> FindOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken);
}
