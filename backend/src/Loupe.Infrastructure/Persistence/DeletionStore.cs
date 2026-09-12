using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Deletions;
using Loupe.Application.PhotographerSummaries;
using Loupe.Domain.Deletions;
using Loupe.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Loupe.Infrastructure.Persistence;

public sealed class DeletionStore(LibraryDbContext database, TimeProvider clock, IPhotographerSummaryQueue summaries) : IDeletionStore
{
    public async Task<DeletionOperation> DeletePhotographerAsync(Guid id, string ownerId, long revision, CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            await AnalysisAdmissionLock.AcquireAsync(database, ownerId, cancellationToken);
            var previous = await database.Deletions.AsNoTracking().SingleOrDefaultAsync(item => item.OwnerId == ownerId
                && item.ResourceType == "photographer" && item.ResourceId == id, cancellationToken);
            if (previous is not null) return previous;
            await summaries.CancelAsync(id, ownerId, true, cancellationToken);
            var photographer = await database.Photographers.FromSqlInterpolated($"SELECT * FROM photographers WHERE \"Id\" = {id} AND \"OwnerId\" = {ownerId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken) ?? throw new ResourceNotFoundException();
            if (photographer.Revision != revision) throw new RevisionConflictException();
            await database.References.Where(item => item.OwnerId == ownerId && item.PhotographerId == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.PhotographerId, (Guid?)null)
                    .SetProperty(item => item.Revision, item => item.Revision + 1), cancellationToken);
            var now = clock.GetUtcNow();
            var operation = new DeletionOperation { OwnerId = ownerId, ResourceType = "photographer", ResourceId = id, DeletedAt = now, CompletedAt = now, MediaKeys = [] };
            database.Deletions.Add(operation); database.Photographers.Remove(photographer);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken); return operation;
        }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
        catch (Exception exception) when (exception is NpgsqlException { IsTransient: true }
            or DbUpdateException { InnerException: NpgsqlException { IsTransient: true } })
        { throw new ServiceUnavailableException(); }
    }

    public async Task<DeletionOperation> DeleteReferenceAsync(Guid id, string ownerId, long revision, CancellationToken cancellationToken)
    {
        try { return await DeleteReferenceCoreAsync(id, ownerId, revision, cancellationToken); }
        catch (Exception exception) when (exception is NpgsqlException { IsTransient: true }
            or DbUpdateException { InnerException: NpgsqlException { IsTransient: true } })
        { throw new ServiceUnavailableException(); }
    }

    private async Task<DeletionOperation> DeleteReferenceCoreAsync(Guid id, string ownerId, long revision, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Serialize admission and deletion, then lock background work before the reference.
        await AnalysisAdmissionLock.AcquireAsync(database, ownerId, cancellationToken);
        var previous = await database.Deletions.AsNoTracking().SingleOrDefaultAsync(item => item.OwnerId == ownerId
            && item.ResourceType == "reference" && item.ResourceId == id, cancellationToken);
        if (previous is not null) return previous;
        var now = clock.GetUtcNow();
        await database.BackgroundOperations.Where(item => item.OwnerId == ownerId
            && (item.Type == OperationType.ReferenceImport || item.Type == OperationType.ReferenceAnalysis) && item.ResourceId == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, item =>
                    item.Status == OperationStatus.Queued || item.Status == OperationStatus.Running ? OperationStatus.Canceled : item.Status)
                .SetProperty(item => item.InputJson, (string?)null).SetProperty(item => item.OutputJson, (string?)null)
                .SetProperty(item => item.CompletedAt, item => item.CompletedAt ?? now)
                .SetProperty(item => item.LeaseToken, (Guid?)null).SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                .SetProperty(item => item.NextAttemptAt, (DateTimeOffset?)null).SetProperty(item => item.RetryAvailableAt, (DateTimeOffset?)null)
                .SetProperty(item => item.FailureCode, (string?)null).SetProperty(item => item.UpdatedAt, now)
                .SetProperty(item => item.Message, "The reference was deleted."), cancellationToken);
        var reference = await database.References.FromSqlInterpolated($"SELECT * FROM \"references\" WHERE \"Id\" = {id} AND \"OwnerId\" = {ownerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw new ResourceNotFoundException();
        if (reference.Revision != revision) throw new RevisionConflictException();
        var operation = new DeletionOperation
        {
            OwnerId = ownerId,
            ResourceType = "reference",
            ResourceId = id,
            DeletedAt = now,
            MediaKeys = new[] { reference.ImageKey, reference.PreviewKey }.OfType<string>().ToArray()
        };
        database.Deletions.Add(operation);
        database.References.Remove(reference);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
        await transaction.CommitAsync(cancellationToken);
        return operation;
    }

    public async Task<DeletionOperation> DeleteLocationAsync(Guid id, string ownerId, long revision, CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            var previous = await database.Deletions.AsNoTracking().SingleOrDefaultAsync(item => item.OwnerId == ownerId
                && item.ResourceType == "location" && item.ResourceId == id, cancellationToken);
            if (previous is not null) return previous;
            var location = await database.Locations.FromSqlInterpolated($"SELECT * FROM locations WHERE \"Id\" = {id} AND \"OwnerId\" = {ownerId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken) ?? throw new ResourceNotFoundException();
            if (location.Revision != revision) throw new RevisionConflictException();
            var keys = await database.LocationImages.Where(image => image.LocationId == id && image.OwnerId == ownerId)
                .Select(image => new { image.ImageKey, image.PreviewKey }).ToListAsync(cancellationToken);
            var operation = new DeletionOperation
            {
                OwnerId = ownerId,
                ResourceType = "location",
                ResourceId = id,
                DeletedAt = clock.GetUtcNow(),
                MediaKeys = keys.SelectMany(image => new[] { image.ImageKey, image.PreviewKey }).ToArray()
            };
            database.Deletions.Add(operation);
            database.Locations.Remove(location);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return operation;
        }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
        catch (Exception exception) when (exception is NpgsqlException { IsTransient: true }
            or DbUpdateException { InnerException: NpgsqlException { IsTransient: true } })
        { throw new ServiceUnavailableException(); }
    }

    public Task<DeletionOperation?> FindOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
        database.Deletions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken);

    public async Task<DeletionOperation> DeletePhotographAsync(Guid id, string ownerId, long revision, CancellationToken cancellationToken)
    {
        try { return await DeletePhotographCoreAsync(id, ownerId, revision, cancellationToken); }
        catch (Exception exception) when (exception is NpgsqlException { IsTransient: true }
            or DbUpdateException { InnerException: NpgsqlException { IsTransient: true } })
        {
            throw new ServiceUnavailableException();
        }
    }

    private async Task<DeletionOperation> DeletePhotographCoreAsync(Guid id, string ownerId, long revision, CancellationToken cancellationToken)
    {
        var lockKey = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { ownerId, type = "delete-photograph", id })));
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockKey})", cancellationToken);
        var previous = await database.Deletions.AsNoTracking().SingleOrDefaultAsync(item => item.OwnerId == ownerId
            && item.ResourceType == "photograph" && item.ResourceId == id, cancellationToken);
        if (previous is not null) return previous;
        var photograph = await database.Photographs.FromSqlInterpolated($"SELECT * FROM photographs WHERE \"Id\" = {id} AND \"OwnerId\" = {ownerId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken)
            ?? throw new ResourceNotFoundException();
        if (photograph.Revision != revision) throw new RevisionConflictException();
        var operation = new DeletionOperation
        {
            OwnerId = ownerId,
            ResourceType = "photograph",
            ResourceId = id,
            DeletedAt = clock.GetUtcNow(),
            MediaKeys = [photograph.ImageKey, photograph.PreviewKey]
        };
        database.Deletions.Add(operation);
        await database.BackgroundOperations.Where(item => item.OwnerId == ownerId && item.Type == OperationType.Critique && item.ResourceId == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, item =>
                    item.Status == OperationStatus.Queued || item.Status == OperationStatus.Running ? OperationStatus.Canceled : item.Status)
                .SetProperty(item => item.InputJson, (string?)null).SetProperty(item => item.CompletedAt, item => item.CompletedAt ?? operation.DeletedAt)
                .SetProperty(item => item.OutputJson, (string?)null)
                .SetProperty(item => item.LeaseToken, (Guid?)null).SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null)
                .SetProperty(item => item.NextAttemptAt, (DateTimeOffset?)null).SetProperty(item => item.FailureCode, (string?)null)
                .SetProperty(item => item.RetryAvailableAt, (DateTimeOffset?)null)
                .SetProperty(item => item.UpdatedAt, operation.DeletedAt).SetProperty(item => item.Message, "The photograph was deleted."), cancellationToken);
        database.Photographs.Remove(photograph);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
        await transaction.CommitAsync(cancellationToken);
        await database.Entry(operation).ReloadAsync(cancellationToken);
        return operation;
    }
}
