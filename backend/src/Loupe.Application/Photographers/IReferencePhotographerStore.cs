using Loupe.Domain.References;

namespace Loupe.Application.Photographers;

public interface IReferencePhotographerStore
{
    Task<Reference> SetAsync(string ownerId, Guid referenceId, long revision, Guid? photographerId, CancellationToken cancellationToken);
}
