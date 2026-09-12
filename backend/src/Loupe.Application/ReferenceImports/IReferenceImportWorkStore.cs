using Loupe.Domain.Operations;

namespace Loupe.Application.ReferenceImports;

public interface IReferenceImportWorkStore
{
    Task<BackgroundOperation?> ClaimAsync(CancellationToken cancellationToken);
    Task<bool> RenewAsync(BackgroundOperation operation, CancellationToken cancellationToken);
    Task PublishAsync(BackgroundOperation operation, ReferenceSourceResult result, CancellationToken cancellationToken);
}
