using Loupe.Application.Operations;
using Loupe.Domain.Operations;
using Loupe.Infrastructure.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Loupe.Infrastructure.Persistence;

public sealed class OperationLeaseStore(LibraryDbContext database, TimeProvider clock, IOptions<AiOptions> options) : IOperationLeaseStore
{
    public async Task<BackgroundOperation?> ClaimAsync(IReadOnlyList<OperationCapability> capabilities, CancellationToken cancellationToken)
    {
        if (capabilities.Count == 0) return null;
        var types = capabilities.Select(capability => capability.Type.ToString()).ToArray();
        var modes = capabilities.Select(capability => capability.Mode.ToString()).ToArray();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var cursor = await database.AnalysisDispatchCursors.FromSqlRaw("SELECT * FROM analysis_dispatch_cursor WHERE \"Id\" = 1 FOR UPDATE")
            .AsNoTracking().SingleAsync(cancellationToken);
        var now = clock.GetUtcNow();
        if (await database.BackgroundOperations.CountAsync(item => item.Status == OperationStatus.Running && item.LeaseExpiresAt > now, cancellationToken) >= options.Value.MaxConcurrentCalls)
            return null;
        var operation = await database.BackgroundOperations.FromSqlInterpolated($"""
            SELECT candidate.* FROM background_operations candidate
            WHERE EXISTS (SELECT 1 FROM unnest({types}, {modes}) AS capability(type, mode)
                          WHERE capability.type = candidate."Type" AND capability.mode = candidate."Mode")
              AND ((candidate."Status" = 'Queued' AND (candidate."NextAttemptAt" IS NULL OR candidate."NextAttemptAt" <= {now}))
                OR (candidate."Status" = 'Running' AND candidate."LeaseExpiresAt" <= {now}))
              AND (SELECT COUNT(*) FROM background_operations active
                   WHERE active."OwnerId" = candidate."OwnerId" AND active."Status" = 'Running' AND active."LeaseExpiresAt" > {now}) < 2
            ORDER BY (candidate."OwnerId" > {cursor.OwnerId}) DESC, candidate."OwnerId", candidate."CreatedAt", candidate."Id"
            LIMIT 1 FOR UPDATE OF candidate SKIP LOCKED
            """)
            .SingleOrDefaultAsync(cancellationToken);
        if (operation is null) return null;
        await database.AnalysisDispatchCursors.Where(item => item.Id == 1)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.OwnerId, operation.OwnerId), cancellationToken);
        if (operation.Status == OperationStatus.Running)
        {
            if (operation.RecoveryCount >= 1 || operation.AttemptCount >= 3)
            {
                operation.Status = OperationStatus.Failed;
                operation.FailureCode = "worker_interrupted";
                operation.Message = "Processing was interrupted. Your saved content is unchanged; request a retry when processing is available.";
                operation.UpdatedAt = now;
                operation.CompletedAt = now;
                operation.LeaseToken = null;
                operation.LeaseExpiresAt = null;
                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return null;
            }
            operation.RecoveryCount++;
        }
        operation.Status = OperationStatus.Running;
        operation.UpdatedAt = now;
        operation.AttemptCount++;
        operation.NextAttemptAt = null;
        operation.RetryAvailableAt = null;
        operation.FailureCode = null;
        operation.LeaseToken = Guid.NewGuid();
        operation.LeaseExpiresAt = operation.UpdatedAt.AddSeconds(60);
        operation.Message = operation.Type switch
        {
            OperationType.Critique => "Analyzing the submitted photograph.",
            OperationType.ReferenceImport => "Retrieving the permitted source.",
            _ => "Processing saved content."
        };
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        database.Entry(operation).State = EntityState.Detached;
        return operation;
    }

    public async Task<bool> RenewAsync(BackgroundOperation operation, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return await database.BackgroundOperations.Where(item => item.Id == operation.Id && item.LeaseToken == operation.LeaseToken
            && item.Status == OperationStatus.Running && item.LeaseExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.LeaseExpiresAt, now.AddSeconds(60))
                .SetProperty(item => item.UpdatedAt, now), cancellationToken) == 1;
    }

}
