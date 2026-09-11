using Loupe.Domain.References;

namespace Loupe.Application.References;

public interface IReferenceTagStore
{
    Task<Reference> ReplaceAsync(string ownerId, Guid referenceId, long revision, IReadOnlyList<ReferenceTagInput> tags, CancellationToken cancellationToken);
}
