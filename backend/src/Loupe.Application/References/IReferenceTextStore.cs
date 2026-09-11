using Loupe.Domain.References;

namespace Loupe.Application.References;

public interface IReferenceTextStore
{
    Task<Reference> UpdateAsync(Guid id, string ownerId, long revision, ReferenceTextField field, string? text, CancellationToken cancellationToken);
}
