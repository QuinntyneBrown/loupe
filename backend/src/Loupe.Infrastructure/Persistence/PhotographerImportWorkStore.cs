using System.Text.Json;
using Loupe.Application.Operations;
using Loupe.Application.PhotographerImports;
using Loupe.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class PhotographerImportWorkStore(LibraryDbContext database, IOperationLeaseStore leases, TimeProvider clock) : IPhotographerImportWorkStore
{
    public Task<BackgroundOperation?> ClaimAsync(CancellationToken cancellationToken) => leases.ClaimAsync([new(OperationType.PhotographerDraftImport, ExecutionMode.Live)], cancellationToken);
    public Task<bool> RenewAsync(BackgroundOperation operation, CancellationToken cancellationToken) => leases.RenewAsync(operation, cancellationToken);

    public async Task PublishAsync(BackgroundOperation operation, PortfolioSourceResult result, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var current = await database.BackgroundOperations.FromSqlInterpolated($"SELECT * FROM background_operations WHERE \"Id\" = {operation.Id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        var now = clock.GetUtcNow();
        if (current is null || current.Status != OperationStatus.Running || current.LeaseToken != operation.LeaseToken || current.LeaseExpiresAt <= now) return;
        var draft = await database.PhotographerDrafts.FromSqlInterpolated($"SELECT * FROM photographer_drafts WHERE \"Id\" = {operation.ResourceId} AND \"OwnerId\" = {operation.OwnerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (draft is null || draft.Canceled || draft.ExpiresAt <= now || draft.CommittedPhotographerId is not null || draft.ImportOperationId != operation.Id)
        {
            Finish(current, OperationStatus.Canceled, now, "Page preview is no longer available."); current.InputJson = null; current.OutputJson = null;
        }
        else if (result.FailureCode is "source_unavailable" or "source_timeout" && operation.AttemptCount < 3)
        {
            current.Status = OperationStatus.Queued; current.FailureCode = result.FailureCode; current.NextAttemptAt = now.AddSeconds(operation.AttemptCount == 1 ? 5 : 30);
            current.UpdatedAt = now; current.LeaseToken = null; current.LeaseExpiresAt = null; current.Message = "The page is temporarily unavailable. Waiting to try again.";
        }
        else
        {
            draft.SourceJson = result.Source is null ? null : JsonSerializer.Serialize(result.Source);
            draft.Name ??= result.Source?.Title; draft.FailureCode = result.FailureCode; draft.Revision++;
            Finish(current, result.Source is null ? OperationStatus.Failed : OperationStatus.Succeeded, now,
                result.Source is null ? "Could not read this page. You can add the details yourself." : "Page details are ready to review.");
            current.FailureCode = result.FailureCode;
        }
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }

    private static void Finish(BackgroundOperation operation, OperationStatus status, DateTimeOffset now, string message)
    {
        operation.Status = status; operation.CompletedAt = now; operation.UpdatedAt = now; operation.Message = message;
        operation.LeaseToken = null; operation.LeaseExpiresAt = null; operation.NextAttemptAt = null; operation.RetryAvailableAt = null;
    }
}
