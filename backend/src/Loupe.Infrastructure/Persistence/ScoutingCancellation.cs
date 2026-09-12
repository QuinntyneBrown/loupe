using Loupe.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

/// <summary>Cancels the location's active scouting job so a late result cannot commit against a changed image set or a deleted location.</summary>
public static class ScoutingCancellation
{
    public static async Task CancelAsync(LibraryDbContext database, Guid locationId, string ownerId, string message, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await AnalysisAdmissionLock.AcquireAsync(database, ownerId, cancellationToken);
        await database.BackgroundOperations.Where(operation => operation.ResourceId == locationId && operation.OwnerId == ownerId
            && operation.Type == OperationType.LocationScouting
            && (operation.Status == OperationStatus.Queued || operation.Status == OperationStatus.Running))
            .ExecuteUpdateAsync(setters => setters.SetProperty(operation => operation.Status, OperationStatus.Canceled)
                .SetProperty(operation => operation.LeaseToken, (Guid?)null).SetProperty(operation => operation.LeaseExpiresAt, (DateTimeOffset?)null)
                .SetProperty(operation => operation.InputJson, (string?)null).SetProperty(operation => operation.OutputJson, (string?)null)
                .SetProperty(operation => operation.NextAttemptAt, (DateTimeOffset?)null).SetProperty(operation => operation.RetryAvailableAt, (DateTimeOffset?)null)
                .SetProperty(operation => operation.Message, message)
                .SetProperty(operation => operation.CompletedAt, now).SetProperty(operation => operation.UpdatedAt, now), cancellationToken);
    }
}
