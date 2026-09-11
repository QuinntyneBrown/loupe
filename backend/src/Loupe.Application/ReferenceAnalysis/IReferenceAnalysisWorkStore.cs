using Loupe.Domain.Operations;
using Loupe.Domain.References;

namespace Loupe.Application.ReferenceAnalysis;

public interface IReferenceAnalysisWorkStore
{
    Task<BackgroundOperation?> ClaimAsync(ExecutionMode mode, CancellationToken cancellationToken);
    Task<bool> RenewAsync(BackgroundOperation operation, CancellationToken cancellationToken);
    Task PublishAsync(BackgroundOperation operation, ReferenceAnalysisResult result, CancellationToken cancellationToken);
}
