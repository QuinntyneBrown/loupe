using Loupe.Domain.Critiques;
using Loupe.Domain.Operations;
using Loupe.Application.Operations;

namespace Loupe.Application.Critiques;

public interface ICritiqueWorkStore
{
    Task<BackgroundOperation?> ClaimAsync(ExecutionMode mode, CancellationToken cancellationToken);
    Task<bool> RenewAsync(BackgroundOperation operation, CancellationToken cancellationToken);
    Task PublishAsync(BackgroundOperation operation, CritiqueResult result, CancellationToken cancellationToken);
    Task RejectInvalidAsync(BackgroundOperation operation, CancellationToken cancellationToken);
    Task RejectTimeoutAsync(BackgroundOperation operation, CancellationToken cancellationToken);
    Task RejectProviderAsync(BackgroundOperation operation, ProviderFailureException failure, CancellationToken cancellationToken);
}
