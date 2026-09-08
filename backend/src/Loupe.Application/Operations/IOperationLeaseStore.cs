using Loupe.Domain.Operations;

namespace Loupe.Application.Operations;

public interface IOperationLeaseStore
{
    Task<BackgroundOperation?> ClaimAsync(IReadOnlyList<OperationCapability> capabilities, CancellationToken cancellationToken);
    Task<bool> RenewAsync(BackgroundOperation operation, CancellationToken cancellationToken);
}
