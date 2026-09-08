using Loupe.Domain.Photographs;
using Loupe.Application.Common;

namespace Loupe.Application.Photographs;

public interface IPhotographStore
{
    Task SaveAsync(Photograph photograph, CancellationToken cancellationToken);
    Task<IReadOnlyList<PhotographSummary>> ListAsync(string ownerId, int count, CreatedCursor? cursor, CancellationToken cancellationToken);
    Task<IReadOnlyList<PhotographSummary>> ListEligibleAsync(string ownerId, int count, CreatedCursor? cursor, CancellationToken cancellationToken);
    Task<Photograph?> FindOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken);
    Task<Photograph> UpdateBriefAsync(Guid id, string ownerId, long revision, CritiqueBrief brief, CancellationToken cancellationToken);
    Task<Photograph> UpdateNotesAsync(Guid id, string ownerId, long revision, string? notes, CancellationToken cancellationToken);
}
