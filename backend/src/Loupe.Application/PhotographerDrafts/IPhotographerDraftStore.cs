using Loupe.Domain.Photographers;
using Loupe.Application.Photographers;

namespace Loupe.Application.PhotographerDrafts;

public interface IPhotographerDraftStore
{
    Task<Guid> AdmitAsync(string ownerId, string portfolioUrl, string? name, bool configured, CancellationToken cancellationToken);
    Task<PhotographerDraft> FindOwnedAsync(string ownerId, Guid id, CancellationToken cancellationToken);
    Task CancelAsync(string ownerId, Guid id, CancellationToken cancellationToken);
    Task<SavePhotographerResult> CommitAsync(string ownerId, Guid id, long revision, PhotographerMetadata metadata, CancellationToken cancellationToken);
}
