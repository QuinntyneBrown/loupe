using Loupe.Domain.Operations;
using Loupe.Domain.References;

namespace Loupe.Application.ReferenceAnalysis;

public interface IReferenceAnalysisProvider
{
    Task<ReferenceAnalysisResult> GenerateAsync(ReferenceAnalysisInput input, AnalysisIdentity identity, CancellationToken cancellationToken);
}
