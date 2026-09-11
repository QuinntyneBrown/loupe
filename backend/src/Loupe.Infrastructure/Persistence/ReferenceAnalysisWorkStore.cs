using System.Text.Json;
using Loupe.Application.Operations;
using Loupe.Application.ReferenceAnalysis;
using Loupe.Domain.Operations;
using Loupe.Domain.References;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class ReferenceAnalysisWorkStore(LibraryDbContext database, IOperationLeaseStore leases, TimeProvider clock) : IReferenceAnalysisWorkStore
{
    public Task<BackgroundOperation?> ClaimAsync(ExecutionMode mode, CancellationToken cancellationToken) => leases.ClaimAsync([new(OperationType.ReferenceAnalysis, mode)], cancellationToken);
    public Task<bool> RenewAsync(BackgroundOperation operation, CancellationToken cancellationToken) => leases.RenewAsync(operation, cancellationToken);
    public async Task PublishAsync(BackgroundOperation operation, ReferenceAnalysisResult result, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var current = await database.BackgroundOperations.FromSqlInterpolated($"SELECT * FROM background_operations WHERE \"Id\" = {operation.Id} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        var now = clock.GetUtcNow();
        if (current is null || current.Status != OperationStatus.Running || current.LeaseToken != operation.LeaseToken || current.LeaseExpiresAt <= now) return;
        var reference = await database.References.FromSqlInterpolated($"SELECT * FROM \"references\" WHERE \"Id\" = {operation.ResourceId} AND \"OwnerId\" = {operation.OwnerId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        var input = JsonSerializer.Deserialize<ReferenceAnalysisInput>(operation.InputJson!)!;
        if (reference is null || reference.CurrentAnalysisOperationId != operation.Id || reference.ImageRevision != input.ImageRevision || reference.ImageKey != input.ImageKey) return;
        var saved = new SavedReferenceSuggestions(operation.Id, input.ImageRevision, now, operation.Mode, operation.Model, operation.PromptVersion,
            result.Description.Trim(), "pending", result.Tags.Select(tag => new ReferenceSuggestedTag(tag.Name.Trim().Normalize(), tag.Category)).ToArray());
        var json = JsonSerializer.Serialize(saved);
        reference.SuggestionsJson = json;
        reference.Revision++;
        current.OutputJson = json;
        current.Status = OperationStatus.Succeeded; current.CompletedAt = now; current.UpdatedAt = now;
        current.LeaseToken = null; current.LeaseExpiresAt = null;
        current.Message = "Suggestions ready. Nothing applies until you accept it.";
        await database.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }
}
