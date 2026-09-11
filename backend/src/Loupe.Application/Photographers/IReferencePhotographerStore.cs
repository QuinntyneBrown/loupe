using Loupe.Domain.References;
using Loupe.Application.Common;
using Loupe.Application.References;

namespace Loupe.Application.Photographers;

public interface IReferencePhotographerStore
{
    Task<IReadOnlyList<ReferenceCandidate>> CandidatesAsync(string ownerId, string query, int count, CreatedCursor? cursor, CancellationToken cancellationToken);
    Task<int> CountCandidatesAsync(string ownerId, string query, CancellationToken cancellationToken);
    Task<Reference> SetAsync(string ownerId, Guid referenceId, long revision, Guid? photographerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReferenceSummary>> ListAsync(string ownerId, Guid photographerId, int count, CreatedCursor? cursor, CancellationToken cancellationToken);
    Task<int> CountAsync(string ownerId, Guid photographerId, CancellationToken cancellationToken);
}
