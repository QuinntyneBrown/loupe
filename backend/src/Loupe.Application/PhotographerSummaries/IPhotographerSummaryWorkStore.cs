using Loupe.Domain.Operations;
using Loupe.Domain.Photographers;

namespace Loupe.Application.PhotographerSummaries;

public interface IPhotographerSummaryWorkStore
{
    Task<BackgroundOperation?> ClaimAsync(ExecutionMode mode, CancellationToken cancellationToken);
    Task<bool> RenewAsync(BackgroundOperation operation, CancellationToken cancellationToken);
    Task PublishAsync(BackgroundOperation operation, CapturedPortfolioPage? source, PhotographerSummaryResult? result, string? sourceFailure, CancellationToken cancellationToken);
}
