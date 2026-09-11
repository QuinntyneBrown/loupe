using Loupe.Domain.Operations;

namespace Loupe.Application.ReferenceAnalysis;

public interface IReferenceAnalysisStore
{
    Task<Guid> AdmitAsync(Guid id, string ownerId, long revision, AnalysisIdentity identity, CancellationToken cancellationToken);
}
