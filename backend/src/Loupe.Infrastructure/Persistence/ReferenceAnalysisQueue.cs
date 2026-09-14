using System.Text.Json;
using Loupe.Application.ReferenceAnalysis;
using Loupe.Domain.Operations;
using Loupe.Domain.References;
using Loupe.Infrastructure.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Loupe.Infrastructure.Persistence;

public sealed class ReferenceAnalysisQueue(LibraryDbContext database, IReferenceAnalysisConfiguration configuration, IOptions<AiOptions> options, TimeProvider clock) : IReferenceAnalysisQueue
{
    public async Task QueueIfConfiguredAsync(Reference reference, CancellationToken cancellationToken)
    {
        if (!options.Value.IsConfigured || reference.ImageKey is null || reference.PreviewKey is null) return;
        await AnalysisAdmissionLock.AcquireAsync(database, reference.OwnerId, cancellationToken);
        var identity = configuration.GetIdentity();
        var now = clock.GetUtcNow();
        var full = await database.BackgroundOperations.CountAsync(operation => operation.OwnerId == reference.OwnerId
            && operation.Type != OperationType.LocationIndex && (operation.Status == OperationStatus.Queued || operation.Status == OperationStatus.Running), cancellationToken) >= 5;
        var operation = new BackgroundOperation
        {
            OwnerId = reference.OwnerId, ResourceId = reference.Id, Type = OperationType.ReferenceAnalysis, Mode = identity.Mode,
            Model = identity.Model, PromptVersion = identity.PromptVersion, CreatedAt = now, UpdatedAt = now,
            InputJson = JsonSerializer.Serialize(new ReferenceAnalysisInput(reference.ImageKey, reference.PreviewKey, reference.ImageRevision)),
            Status = full ? OperationStatus.Failed : OperationStatus.Queued,
            CompletedAt = full ? now : null,
            FailureCode = full ? "analysis_limit" : null,
            Message = full ? "Your image is saved. Too many operations are active; request suggestions when one finishes." : "Waiting to start."
        };
        database.BackgroundOperations.Add(operation);
        reference.CurrentAnalysisOperationId = operation.Id;
    }
}
