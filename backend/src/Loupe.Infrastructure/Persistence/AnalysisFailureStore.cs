using Loupe.Application.Operations;
using Loupe.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class AnalysisFailureStore(LibraryDbContext database, TimeProvider clock) : IAnalysisFailureStore
{
    public Task RejectInvalidAsync(BackgroundOperation operation, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var retry = operation.InvalidOutputCount == 0 && operation.AttemptCount < 3;
        return database.BackgroundOperations.Where(item => item.Id == operation.Id && item.LeaseToken == operation.LeaseToken
            && item.Status == OperationStatus.Running && item.LeaseExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, retry ? OperationStatus.Queued : OperationStatus.Failed)
                .SetProperty(item => item.InvalidOutputCount, item => item.InvalidOutputCount + 1)
                .SetProperty(item => item.NextAttemptAt, retry ? now.AddSeconds(5) : (DateTimeOffset?)null)
                .SetProperty(item => item.CompletedAt, retry ? (DateTimeOffset?)null : now).SetProperty(item => item.UpdatedAt, now)
                .SetProperty(item => item.LeaseToken, (Guid?)null).SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                .SetProperty(item => item.FailureCode, "invalid_output")
                .SetProperty(item => item.Message, retry ? (operation.Type == OperationType.Critique ? "The critique was incomplete. Waiting to try once more." : operation.Type == OperationType.LocationScouting ? "The scouting report was incomplete. Waiting to try once more." : "The suggestions were incomplete. Waiting to try once more.") : (operation.Type == OperationType.Critique ? "The critique could not be validated. Your saved content is unchanged." : operation.Type == OperationType.LocationScouting ? "The scouting report could not be validated. Your saved content is unchanged." : "The suggestions could not be validated. Your saved content is unchanged.")), cancellationToken);
    }

    public Task RejectTimeoutAsync(BackgroundOperation operation, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var retry = operation.AttemptCount < 3;
        var delay = TimeSpan.FromSeconds(operation.AttemptCount == 1 ? 5 : 30);
        return database.BackgroundOperations.Where(item => item.Id == operation.Id && item.LeaseToken == operation.LeaseToken
            && item.Status == OperationStatus.Running && item.LeaseExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, retry ? OperationStatus.Queued : OperationStatus.Failed)
                .SetProperty(item => item.NextAttemptAt, retry ? now.Add(delay) : (DateTimeOffset?)null)
                .SetProperty(item => item.CompletedAt, retry ? (DateTimeOffset?)null : now).SetProperty(item => item.UpdatedAt, now)
                .SetProperty(item => item.LeaseToken, (Guid?)null).SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                .SetProperty(item => item.FailureCode, "provider_timeout")
                .SetProperty(item => item.Message, retry ? "Analysis timed out. Waiting to try again." : "Analysis timed out after three attempts. Your saved content is unchanged."), cancellationToken);
    }

    public Task RejectProviderAsync(BackgroundOperation operation, ProviderFailureException failure, CancellationToken cancellationToken)
    {
        if (failure.Kind == ProviderFailureKind.InvalidOutput) return RejectInvalidAsync(operation, cancellationToken);
        var now = clock.GetUtcNow();
        var transient = failure.Kind is ProviderFailureKind.Transient or ProviderFailureKind.RateLimited;
        var requestedWait = failure.RetryAfter.GetValueOrDefault();
        var retry = transient && requestedWait <= TimeSpan.FromSeconds(300) && operation.AttemptCount < 3;
        var delay = TimeSpan.FromSeconds(operation.AttemptCount == 1 ? 5 : 30);
        if (requestedWait > delay) delay = requestedWait;
        var available = !retry && transient && requestedWait > TimeSpan.Zero
            ? (requestedWait > DateTimeOffset.MaxValue - now ? DateTimeOffset.MaxValue : now.Add(requestedWait)) : (DateTimeOffset?)null;
        var (code, message) = failure.Kind switch
        {
            ProviderFailureKind.Transient => ("provider_unavailable", retry ? "The analysis service is temporarily unavailable. Waiting to try again." : "The analysis service could not complete this request. Your saved content is unchanged."),
            ProviderFailureKind.RateLimited => ("provider_rate_limited", retry ? "The analysis service is busy. Waiting to try again." : available is not null ? "The analysis service requested a wait. Try after the shown time." : "The analysis service is busy. Your saved content is unchanged."),
            ProviderFailureKind.AccessDenied => ("provider_access_denied", "The analysis service denied access. Check the integration configuration or continue using your saved content."),
            ProviderFailureKind.InvalidCredentials => ("provider_credentials", "Analysis credentials are not valid. Update the integration configuration before trying again."),
            ProviderFailureKind.UnsupportedInput => ("unsupported_input", "The analysis service cannot process this input. Your saved content is unchanged."),
            ProviderFailureKind.Disabled => ("provider_disabled", "Analysis is disabled. Enable the integration or continue using your saved content."),
            _ => ("provider_error", "Analysis could not complete. Your saved content is unchanged.")
        };
        return database.BackgroundOperations.Where(item => item.Id == operation.Id && item.LeaseToken == operation.LeaseToken
            && item.Status == OperationStatus.Running && item.LeaseExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, retry ? OperationStatus.Queued : OperationStatus.Failed)
                .SetProperty(item => item.NextAttemptAt, retry ? now.Add(delay) : (DateTimeOffset?)null)
                .SetProperty(item => item.RetryAvailableAt, available)
                .SetProperty(item => item.CompletedAt, retry ? (DateTimeOffset?)null : now).SetProperty(item => item.UpdatedAt, now)
                .SetProperty(item => item.LeaseToken, (Guid?)null).SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                .SetProperty(item => item.FailureCode, code).SetProperty(item => item.Message, message), cancellationToken);
    }

}
