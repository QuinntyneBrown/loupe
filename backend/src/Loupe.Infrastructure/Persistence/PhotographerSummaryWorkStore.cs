using System.Text.Json;
using Loupe.Application.Operations;
using Loupe.Application.PhotographerSummaries;
using Loupe.Domain.Operations;
using Loupe.Domain.Photographers;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class PhotographerSummaryWorkStore(LibraryDbContext database, IOperationLeaseStore leases, TimeProvider clock) : IPhotographerSummaryWorkStore
{
    public Task<BackgroundOperation?> ClaimAsync(ExecutionMode mode, CancellationToken cancellationToken) => leases.ClaimAsync([new(OperationType.PhotographerSummary, mode)], cancellationToken);
    public Task<bool> RenewAsync(BackgroundOperation operation, CancellationToken cancellationToken) => leases.RenewAsync(operation, cancellationToken);
    public async Task PublishAsync(BackgroundOperation operation, CapturedPortfolioPage? source, PhotographerSummaryResult? result, string? sourceFailure, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var current = await database.BackgroundOperations.FromSqlInterpolated($"SELECT * FROM background_operations WHERE \"Id\" = {operation.Id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        var now = clock.GetUtcNow();
        if (current is null || current.Status != OperationStatus.Running || current.LeaseToken != operation.LeaseToken || current.LeaseExpiresAt <= now) return;
        var photographer = await database.Photographers.FromSqlInterpolated($"SELECT * FROM photographers WHERE \"Id\" = {operation.ResourceId} AND \"OwnerId\" = {operation.OwnerId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        var input = JsonSerializer.Deserialize<PhotographerSummaryInput>(operation.InputJson!)!;
        if (photographer is null || photographer.CurrentSummaryOperationId != operation.Id || photographer.SourceRevision != input.SourceRevision)
        {
            Finish(current, OperationStatus.Canceled, now, "The saved portfolio changed or is no longer available.");
            current.InputJson = null; current.OutputJson = null;
        }
        else if (sourceFailure is "source_unavailable" or "source_timeout" && operation.AttemptCount < 3)
        {
            current.Status = OperationStatus.Queued; current.FailureCode = sourceFailure; current.NextAttemptAt = now.AddSeconds(operation.AttemptCount == 1 ? 5 : 30);
            current.UpdatedAt = now; current.LeaseToken = null; current.LeaseExpiresAt = null; current.Message = "The portfolio page is temporarily unavailable. Waiting to try again.";
        }
        else if (source is null || result is null)
        {
            photographer.SourceFailureCode = sourceFailure; photographer.Revision++;
            Finish(current, OperationStatus.Failed, now, "Summary unavailable. The portfolio page could not be read; you can edit the description yourself.");
            current.FailureCode = sourceFailure;
        }
        else
        {
            var saved = new SavedPhotographerSuggestions(operation.Id, input.SourceRevision, now, operation.Mode, operation.Model, operation.PromptVersion,
                source, result.Summary?.Trim(), result.Summary is null ? "unavailable" : "pending",
                result.Tags.Select(tag => new PhotographerSuggestedTag(tag.Name.Trim().Normalize(), tag.Category)).ToArray(), result.UnavailableReason);
            var json = JsonSerializer.Serialize(saved);
            photographer.SourceJson = JsonSerializer.Serialize(source); photographer.CapturedSourceRevision = input.SourceRevision;
            photographer.SourceFailureCode = null; photographer.SuggestionsJson = json; photographer.Revision++;
            photographer.SuggestionUndoJson = null;
            current.OutputJson = json;
            Finish(current, OperationStatus.Succeeded, now, result.Summary is null
                ? "Summary unavailable. This page does not contain enough information."
                : "Summary suggestions are ready. Nothing applies until you accept it.");
        }
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }
    private static void Finish(BackgroundOperation operation, OperationStatus status, DateTimeOffset now, string message)
    {
        operation.Status = status; operation.CompletedAt = now; operation.UpdatedAt = now; operation.Message = message;
        operation.LeaseToken = null; operation.LeaseExpiresAt = null; operation.NextAttemptAt = null; operation.RetryAvailableAt = null;
    }
}
