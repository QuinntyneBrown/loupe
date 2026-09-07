using Loupe.Domain.Photographs;

namespace Loupe.Application.Photographs;

public interface IPhotographStore
{
    Task SaveAsync(Photograph photograph, CancellationToken cancellationToken);
    Task<Photograph?> FindOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken);
    Task<Photograph> UpdateBriefAsync(Guid id, string ownerId, long revision, CritiqueBrief brief, CancellationToken cancellationToken);
    Task<Photograph> UpdateNotesAsync(Guid id, string ownerId, long revision, string? notes, CancellationToken cancellationToken);
}
