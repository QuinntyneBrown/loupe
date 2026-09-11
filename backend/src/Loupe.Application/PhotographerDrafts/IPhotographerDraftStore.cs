using Loupe.Domain.Photographers;

namespace Loupe.Application.PhotographerDrafts;

public interface IPhotographerDraftStore
{
    Task<Guid> AdmitAsync(string ownerId, string portfolioUrl, string? name, bool configured, CancellationToken cancellationToken);
    Task<PhotographerDraft> FindOwnedAsync(string ownerId, Guid id, CancellationToken cancellationToken);
    Task CancelAsync(string ownerId, Guid id, CancellationToken cancellationToken);
}
