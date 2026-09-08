using Loupe.Domain.Operations;

namespace Loupe.Application.Operations;

public interface IBackgroundOperationStore
{
    Task<Guid> AdmitCritiqueAsync(Guid photographId, string ownerId, long revision, bool regenerate, AnalysisIdentity identity, CancellationToken cancellationToken);
    Task<BackgroundOperation?> FindOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken);
}
