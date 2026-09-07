using Loupe.Domain.Critiques;
using Loupe.Domain.Operations;

namespace Loupe.Application.Critiques;

public interface ICritiqueProvider
{
    Task<CritiqueResult> GenerateAsync(CritiqueInput input, AnalysisIdentity identity, CancellationToken cancellationToken);
}
