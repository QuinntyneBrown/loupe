using Loupe.Domain.Locations;
using Loupe.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

/// <summary>Records, in the caller's transaction, that the location's search document must be re-embedded; queued intents coalesce and a running one is superseded.</summary>
public static class LocationIndexIntent
{
    public const string PromptVersion = "location-document-v1";

    public static async Task RecordAsync(LibraryDbContext database, Location location, string model, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var current = location.CurrentIndexOperation;
        if (current is null && location.CurrentIndexOperationId is { } currentId)
            current = await database.BackgroundOperations.SingleOrDefaultAsync(operation => operation.Id == currentId, cancellationToken);
        if (current is { Status: OperationStatus.Queued }) return;
        if (current is { Status: OperationStatus.Running }) Cancel(current, "Superseded by a newer change.", now);
        var intent = new BackgroundOperation
        {
            OwnerId = location.OwnerId,
            ResourceId = location.Id,
            Type = OperationType.LocationIndex,
            Mode = ExecutionMode.Live,
            Model = model,
            PromptVersion = PromptVersion,
            InputJson = null,
            CreatedAt = now,
            UpdatedAt = now,
            Message = "Waiting to update search."
        };
        database.BackgroundOperations.Add(intent);
        location.CurrentIndexOperationId = intent.Id;
        location.CurrentIndexOperation = intent;
    }

    public static Task CancelAsync(LibraryDbContext database, Guid locationId, string ownerId, string message, DateTimeOffset now, CancellationToken cancellationToken) =>
        database.BackgroundOperations.Where(operation => operation.ResourceId == locationId && operation.OwnerId == ownerId
            && operation.Type == OperationType.LocationIndex
            && (operation.Status == OperationStatus.Queued || operation.Status == OperationStatus.Running))
            .ExecuteUpdateAsync(setters => setters.SetProperty(operation => operation.Status, OperationStatus.Canceled)
                .SetProperty(operation => operation.LeaseToken, (Guid?)null).SetProperty(operation => operation.LeaseExpiresAt, (DateTimeOffset?)null)
                .SetProperty(operation => operation.NextAttemptAt, (DateTimeOffset?)null).SetProperty(operation => operation.RetryAvailableAt, (DateTimeOffset?)null)
                .SetProperty(operation => operation.Message, message)
                .SetProperty(operation => operation.CompletedAt, now).SetProperty(operation => operation.UpdatedAt, now), cancellationToken);

    private static void Cancel(BackgroundOperation operation, string message, DateTimeOffset now)
    {
        operation.Status = OperationStatus.Canceled;
        operation.LeaseToken = null;
        operation.LeaseExpiresAt = null;
        operation.NextAttemptAt = null;
        operation.RetryAvailableAt = null;
        operation.Message = message;
        operation.CompletedAt = now;
        operation.UpdatedAt = now;
    }
}
