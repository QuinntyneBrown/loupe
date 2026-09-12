using System.Text.Json;
using Loupe.Application.PhotographerSummaries;
using Loupe.Domain.Operations;
using Loupe.Domain.Photographers;
using Loupe.Infrastructure.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Loupe.Infrastructure.Persistence;

public sealed class PhotographerSummaryQueue(LibraryDbContext database, IPhotographerSummaryConfiguration configuration,
    IOptions<AiOptions> options, TimeProvider clock) : IPhotographerSummaryQueue
{
    public async Task QueueIfConfiguredAsync(Photographer photographer, CancellationToken cancellationToken)
    {
        if (!options.Value.IsConfigured) return;
        await AnalysisAdmissionLock.AcquireAsync(database, photographer.OwnerId, cancellationToken);
        var identity = configuration.GetIdentity(); var now = clock.GetUtcNow();
        var full = await database.BackgroundOperations.CountAsync(operation => operation.OwnerId == photographer.OwnerId
            && operation.Type != OperationType.LocationIndex && (operation.Status == OperationStatus.Queued || operation.Status == OperationStatus.Running), cancellationToken) >= 5;
        var source = photographer.CapturedSourceRevision == photographer.SourceRevision && photographer.SourceJson is not null
            ? JsonSerializer.Deserialize<CapturedPortfolioPage>(photographer.SourceJson) : null;
        var operation = new BackgroundOperation
        {
            OwnerId = photographer.OwnerId, ResourceId = photographer.Id, Type = OperationType.PhotographerSummary, Mode = identity.Mode,
            Model = identity.Model, PromptVersion = identity.PromptVersion, CreatedAt = now, UpdatedAt = now,
            InputJson = JsonSerializer.Serialize(new PhotographerSummaryInput(photographer.PortfolioUrl, photographer.SourceRevision, source)),
            Status = full ? OperationStatus.Failed : OperationStatus.Queued, CompletedAt = full ? now : null,
            FailureCode = full ? "analysis_limit" : null,
            Message = full ? "Your photographer is saved. Too many operations are active; request a summary when one finishes." : "Waiting to start."
        };
        database.BackgroundOperations.Add(operation); photographer.CurrentSummaryOperationId = operation.Id;
    }

    public Task CancelAsync(Guid id, string ownerId, bool deleting, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        // Call before locking the bookmark, matching the worker's operation-then-resource lock order.
        return database.BackgroundOperations.Where(operation => operation.OwnerId == ownerId && operation.ResourceId == id
            && operation.Type == OperationType.PhotographerSummary
            && (deleting || operation.Status == OperationStatus.Queued || operation.Status == OperationStatus.Running))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(operation => operation.Status, operation => operation.Status == OperationStatus.Queued || operation.Status == OperationStatus.Running ? OperationStatus.Canceled : operation.Status)
                .SetProperty(operation => operation.CompletedAt, operation => operation.CompletedAt ?? now)
                .SetProperty(operation => operation.UpdatedAt, now)
                .SetProperty(operation => operation.LeaseToken, (Guid?)null).SetProperty(operation => operation.LeaseExpiresAt, (DateTimeOffset?)null)
                .SetProperty(operation => operation.NextAttemptAt, (DateTimeOffset?)null).SetProperty(operation => operation.RetryAvailableAt, (DateTimeOffset?)null)
                .SetProperty(operation => operation.InputJson, (string?)null).SetProperty(operation => operation.OutputJson, (string?)null)
                .SetProperty(operation => operation.Message, deleting ? "The photographer was deleted." : "The portfolio URL changed."), cancellationToken);
    }
}
