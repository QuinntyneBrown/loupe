using Loupe.Application.Images;
using Loupe.Application.Operations;
using Loupe.Application.ReferenceImports;
using Loupe.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class ReferenceImportWorkStore(LibraryDbContext database, IOperationLeaseStore leases, IImageStore images, TimeProvider clock) : IReferenceImportWorkStore
{
    public Task<BackgroundOperation?> ClaimAsync(CancellationToken cancellationToken) =>
        leases.ClaimAsync([new(OperationType.ReferenceDraftImport, ExecutionMode.Live)], cancellationToken);

    public Task<bool> RenewAsync(BackgroundOperation operation, CancellationToken cancellationToken) => leases.RenewAsync(operation, cancellationToken);

    public async Task PublishAsync(BackgroundOperation operation, ReferenceSourceResult result, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await MediaTransactionLock.ProtectUploadAsync(database, cancellationToken);
        var current = await database.BackgroundOperations.FromSqlInterpolated($"SELECT * FROM background_operations WHERE \"Id\" = {operation.Id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        var now = clock.GetUtcNow();
        if (current is null || current.Status != OperationStatus.Running || current.LeaseToken != operation.LeaseToken || current.LeaseExpiresAt <= now) return;
        var draft = await database.ReferenceDrafts.FromSqlInterpolated($"SELECT * FROM reference_drafts WHERE \"Id\" = {operation.ResourceId} AND \"OwnerId\" = {operation.OwnerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (draft is null || draft.ExpiresAt <= now || draft.CommittedReferenceId is not null || draft.ImportOperationId != operation.Id) return;
        var retry = result.FailureCode is "source_unavailable" or "source_timeout" && operation.AttemptCount < 3;
        if (retry)
        {
            current.Status = OperationStatus.Queued;
            current.FailureCode = result.FailureCode;
            current.NextAttemptAt = now.AddSeconds(operation.AttemptCount == 1 ? 5 : 30);
            current.UpdatedAt = now;
            current.LeaseToken = null;
            current.LeaseExpiresAt = null;
            current.Message = "The source is temporarily unavailable. Waiting to try again.";
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }
        // Failed transactions leave media for reference-aware orphan cleanup.
        if (result.Image is { } image)
        {
            var imageKey = await images.WriteAsync(image.Image, cancellationToken);
            var previewKey = await images.WriteAsync(image.Preview, cancellationToken);
            draft.ImageKey = imageKey;
            draft.PreviewKey = previewKey;
            draft.Width = image.Width;
            draft.Height = image.Height;
        }
        draft.Title = result.Title ?? draft.Title;
        draft.Attribution = result.Attribution;
        draft.FailureCode = result.FailureCode;
        draft.Revision++;
        current.Status = result.FailureCode is null ? OperationStatus.Succeeded : OperationStatus.Failed;
        current.FailureCode = result.FailureCode;
        current.Message = result.FailureCode is null ? "Preview ready. Review it before saving." : "A preview could not be imported. You can save the link or add an image yourself.";
        current.OutputJson = System.Text.Json.JsonSerializer.Serialize(new { result.FetchedUrl, retrievedAt = now });
        current.CompletedAt = now;
        current.UpdatedAt = now;
        current.LeaseToken = null;
        current.LeaseExpiresAt = null;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

    }
}
