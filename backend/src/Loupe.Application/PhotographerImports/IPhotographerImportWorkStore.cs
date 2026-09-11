using Loupe.Domain.Operations;

namespace Loupe.Application.PhotographerImports;

public interface IPhotographerImportWorkStore
{
    Task<BackgroundOperation?> ClaimAsync(CancellationToken cancellationToken);
    Task<bool> RenewAsync(BackgroundOperation operation, CancellationToken cancellationToken);
    Task PublishAsync(BackgroundOperation operation, PortfolioSourceResult result, CancellationToken cancellationToken);
}
