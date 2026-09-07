using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Deletions;
using Loupe.Domain.Deletions;
using Loupe.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Loupe.Infrastructure.Persistence;

public sealed class DeletionStore(LibraryDbContext database, TimeProvider clock) : IDeletionStore
{
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
        await database.BackgroundOperations.Where(item => item.OwnerId == ownerId && item.Type == OperationType.Critique && item.ResourceId == id
            && (item.Status == OperationStatus.Queued || item.Status == OperationStatus.Running))
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, OperationStatus.Canceled)
                .SetProperty(item => item.InputJson, (string?)null).SetProperty(item => item.CompletedAt, operation.DeletedAt)
                .SetProperty(item => item.UpdatedAt, operation.DeletedAt).SetProperty(item => item.Message, "The photograph was deleted."), cancellationToken);
        database.Photographs.Remove(photograph);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
        await transaction.CommitAsync(cancellationToken);
        await database.Entry(operation).ReloadAsync(cancellationToken);
        return operation;
    }
}
