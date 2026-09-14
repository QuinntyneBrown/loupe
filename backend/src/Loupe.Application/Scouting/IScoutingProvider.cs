using Loupe.Domain.Operations;
using Loupe.Domain.Scouting;

namespace Loupe.Application.Scouting;

public interface IScoutingProvider
{
    Task<ScoutingReport> GenerateAsync(ScoutingInput input, AnalysisIdentity identity, CancellationToken cancellationToken);
}
