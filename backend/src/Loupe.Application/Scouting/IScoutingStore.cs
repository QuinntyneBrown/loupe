using Loupe.Domain.Operations;

namespace Loupe.Application.Scouting;

public interface IScoutingStore
{
    Task<Guid> AdmitAsync(Guid locationId, string ownerId, long revision, bool regenerate, AnalysisIdentity identity, CancellationToken cancellationToken);
}
