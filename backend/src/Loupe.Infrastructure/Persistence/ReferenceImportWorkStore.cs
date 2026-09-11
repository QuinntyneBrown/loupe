using System.Text.Json;
using Loupe.Application.Images;
using Loupe.Application.Operations;
using Loupe.Application.ReferenceAnalysis;
using Loupe.Application.ReferenceImports;
using Loupe.Domain.Operations;
using Loupe.Domain.References;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class ReferenceImportWorkStore(LibraryDbContext database, IOperationLeaseStore leases, IImageStore images,
    IReferenceAnalysisQueue analysis, TimeProvider clock) : IReferenceImportWorkStore
{
    public Task<BackgroundOperation?> ClaimAsync(CancellationToken cancellationToken) =>
        leases.ClaimAsync([new(OperationType.ReferenceDraftImport, ExecutionMode.Live), new(OperationType.ReferenceImport, ExecutionMode.Live)], cancellationToken);

    public Task<bool> RenewAsync(BackgroundOperation operation, CancellationToken cancellationToken) => leases.RenewAsync(operation, cancellationToken);

    public async Task PublishAsync(BackgroundOperation operation, ReferenceSourceResult result, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await MediaTransactionLock.ProtectUploadAsync(database, cancellationToken);
        // Match image replacement/admission lock order before completing an import and queuing visual analysis.
        if (operation.Type == OperationType.ReferenceImport) await AnalysisAdmissionLock.AcquireAsync(database, operation.OwnerId, cancellationToken);
        var current = await database.BackgroundOperations.FromSqlInterpolated($"SELECT * FROM background_operations WHERE \"Id\" = {operation.Id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        var now = clock.GetUtcNow();
        if (current is null || current.Status != OperationStatus.Running || current.LeaseToken != operation.LeaseToken || current.LeaseExpiresAt <= now) return;
        var input = JsonSerializer.Deserialize<ReferenceImportInput>(operation.InputJson!)!;
        ReferenceDraft? draft = null;
        Reference? reference = null;
        var stale = false;
        if (operation.Type == OperationType.ReferenceDraftImport)
        {
            draft = await database.ReferenceDrafts.FromSqlInterpolated($"SELECT * FROM reference_drafts WHERE \"Id\" = {operation.ResourceId} AND \"OwnerId\" = {operation.OwnerId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
            stale = draft is null || draft.ExpiresAt <= now || draft.CommittedReferenceId is not null || draft.ImportOperationId != operation.Id;
        }
        else
        {
            reference = await database.References.FromSqlInterpolated($"SELECT * FROM \"references\" WHERE \"Id\" = {operation.ResourceId} AND \"OwnerId\" = {operation.OwnerId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
            var source = reference?.SourceUrl;
            var normalized = source is null ? null : await database.Database.SqlQuery<string>($"SELECT loupe_normalize_source({source}) AS \"Value\"").SingleAsync(cancellationToken);
            stale = reference is null || reference.CurrentImportOperationId != operation.Id || reference.ImageKey != input.ImageKey || normalized != input.NormalizedSource;
        }
        if (stale)
        {
            Finish(current, OperationStatus.Canceled, now, "The saved source or image changed. Import canceled.");
            current.InputJson = null; current.OutputJson = null;
            await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return;
        }
        if (result.FailureCode is "source_unavailable" or "source_timeout" && operation.AttemptCount < 3)
        {
            current.Status = OperationStatus.Queued; current.FailureCode = result.FailureCode;
            current.NextAttemptAt = now.AddSeconds(operation.AttemptCount == 1 ? 5 : 30);
            current.UpdatedAt = now; current.LeaseToken = null; current.LeaseExpiresAt = null;
            current.Message = "The source is temporarily unavailable. Waiting to try again.";
            await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return;
        }
        var provenance = result.FailureCode is null or "image_not_found"
            ? JsonSerializer.Serialize(new ImportedReferenceSource(input.SourceUrl, result.FetchedUrl, now, result.Title, result.Attribution)) : null;
        var queueAnalysis = false;
        if (draft is not null)
        {
            if (result.Image is { } image)
            {
                draft.ImageKey = await images.WriteAsync(image.Image, cancellationToken);
                draft.PreviewKey = await images.WriteAsync(image.Preview, cancellationToken);
                draft.Width = image.Width; draft.Height = image.Height;
            }
            draft.Title = result.Title ?? draft.Title; draft.Attribution = result.Attribution;
            draft.FailureCode = result.FailureCode; draft.SourceImportJson = provenance; draft.Revision++;
        }
        else if (reference is not null)
        {
            if (reference.ImageKey is null && result.Image is { } image)
            {
                reference.ImageKey = await images.WriteAsync(image.Image, cancellationToken);
                reference.PreviewKey = await images.WriteAsync(image.Preview, cancellationToken);
                reference.Width = image.Width; reference.Height = image.Height; reference.ImageRevision++;
                queueAnalysis = true;
            }
            if (provenance is not null) reference.SourceImportJson = provenance;
            reference.Revision++;
        }
        Finish(current, result.FailureCode is null ? OperationStatus.Succeeded : OperationStatus.Failed, now,
            result.FailureCode is not null ? "A preview could not be imported. You can keep the link or add an image yourself."
                : draft is not null ? "Preview ready. Review it before saving." : "Source imported. Your saved metadata and notes are unchanged.");
        current.FailureCode = result.FailureCode; current.OutputJson = provenance;
        await database.SaveChangesAsync(cancellationToken);
        if (queueAnalysis)
        {
            await analysis.QueueIfConfiguredAsync(reference!, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
        }
        // Failed transactions leave staged media for reference-aware orphan cleanup.
        await transaction.CommitAsync(cancellationToken);
    }

    private static void Finish(BackgroundOperation operation, OperationStatus status, DateTimeOffset now, string message)
    {
        operation.Status = status; operation.Message = message; operation.CompletedAt = now; operation.UpdatedAt = now;
        operation.LeaseToken = null; operation.LeaseExpiresAt = null; operation.NextAttemptAt = null; operation.RetryAvailableAt = null; operation.FailureCode = null;
    }
}
