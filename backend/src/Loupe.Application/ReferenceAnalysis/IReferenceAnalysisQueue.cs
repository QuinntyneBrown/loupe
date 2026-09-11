using Loupe.Domain.References;

namespace Loupe.Application.ReferenceAnalysis;

public interface IReferenceAnalysisQueue
{
    Task QueueIfConfiguredAsync(Reference reference, CancellationToken cancellationToken);
}
