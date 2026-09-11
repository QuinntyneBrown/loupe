using Loupe.Domain.Operations;

namespace Loupe.Application.PhotographerSummaries;

public interface IPhotographerSummaryStore
{
    Task<Guid> AdmitAsync(Guid id, string ownerId, long revision, AnalysisIdentity identity, CancellationToken cancellationToken);
}
