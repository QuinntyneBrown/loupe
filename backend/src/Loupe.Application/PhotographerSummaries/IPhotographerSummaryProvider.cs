using Loupe.Domain.Operations;
using Loupe.Domain.Photographers;

namespace Loupe.Application.PhotographerSummaries;

public interface IPhotographerSummaryProvider
{
    Task<PhotographerSummaryResult> GenerateAsync(CapturedPortfolioPage source, AnalysisIdentity identity, CancellationToken cancellationToken);
}
