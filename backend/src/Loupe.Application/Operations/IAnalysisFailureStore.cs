using Loupe.Domain.Operations;

namespace Loupe.Application.Operations;

public interface IAnalysisFailureStore
{
    Task RejectInvalidAsync(BackgroundOperation operation, CancellationToken cancellationToken);
    Task RejectTimeoutAsync(BackgroundOperation operation, CancellationToken cancellationToken);
    Task RejectProviderAsync(BackgroundOperation operation, ProviderFailureException failure, CancellationToken cancellationToken);
}
