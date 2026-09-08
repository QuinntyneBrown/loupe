using System.Text.Json;
using Loupe.Application.Critiques;
using Loupe.Domain.Critiques;
using Loupe.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class CritiqueWorkStore(LibraryDbContext database, TimeProvider clock) : ICritiqueWorkStore
{
    public async Task<BackgroundOperation?> ClaimAsync(ExecutionMode mode, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var operation = await database.BackgroundOperations.FromSqlInterpolated($"SELECT * FROM background_operations WHERE \"Type\" = 'Critique' AND \"Mode\" = {mode.ToString()} AND \"Status\" = 'Queued' AND (\"NextAttemptAt\" IS NULL OR \"NextAttemptAt\" <= {now}) ORDER BY \"CreatedAt\", \"Id\" LIMIT 1 FOR UPDATE SKIP LOCKED")
            .SingleOrDefaultAsync(cancellationToken);
        if (operation is null) return null;
        operation.Status = OperationStatus.Running;
        operation.UpdatedAt = now;
        operation.AttemptCount++;
        operation.NextAttemptAt = null;
        operation.FailureCode = null;
        operation.LeaseToken = Guid.NewGuid();
        operation.LeaseExpiresAt = operation.UpdatedAt.AddSeconds(60);
        operation.Message = "Analyzing the submitted photograph.";
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        database.Entry(operation).State = EntityState.Detached;
        return operation;
    }

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
                .SetProperty(item => item.Message, retry ? "The critique was incomplete. Waiting to try once more." : "The critique could not be validated. Your saved content is unchanged."), cancellationToken);
    }

    public async Task PublishAsync(BackgroundOperation operation, CritiqueResult result, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var photograph = await database.Photographs.FromSqlInterpolated($"SELECT * FROM photographs WHERE \"Id\" = {operation.ResourceId} AND \"OwnerId\" = {operation.OwnerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (photograph is null) return;
        var now = clock.GetUtcNow();
        var changed = await database.BackgroundOperations.Where(item => item.Id == operation.Id && item.LeaseToken == operation.LeaseToken
            && item.Status == OperationStatus.Running && item.LeaseExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, OperationStatus.Succeeded)
                .SetProperty(item => item.CompletedAt, now).SetProperty(item => item.UpdatedAt, now)
                .SetProperty(item => item.LeaseToken, (Guid?)null).SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                .SetProperty(item => item.Message, "Critique saved."), cancellationToken);
        if (changed == 0) return;
        var input = JsonSerializer.Deserialize<CritiqueInput>(operation.InputJson!)!;
        photograph.CritiqueJson = JsonSerializer.Serialize(new SavedCritique(operation.Id, now, operation.Mode,
            operation.Model, operation.PromptVersion, input.Brief, result));
        photograph.Revision++;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
