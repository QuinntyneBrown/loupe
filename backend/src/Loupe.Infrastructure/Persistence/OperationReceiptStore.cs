using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Operations;
using Loupe.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class OperationReceiptStore(LibraryDbContext database, TimeProvider clock) : IOperationReceiptStore
{
    public async Task<Guid> ExecuteAsync(string ownerId, string operationType, string key, string payloadHash,
        Func<CancellationToken, Task<Guid>> create, CancellationToken cancellationToken)
    {
        var scopeHash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { ownerId, operationType, key }));
        var lockKey = BinaryPrimitives.ReadInt64BigEndian(scopeHash);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockKey})", cancellationToken);
        var receipt = await database.OperationReceipts.SingleOrDefaultAsync(item => item.OwnerId == ownerId
            && item.OperationType == operationType && item.Key == key, cancellationToken);
        if (receipt is not null && receipt.CreatedAt > clock.GetUtcNow() - TimeSpan.FromHours(24))
        {
            if (receipt.PayloadHash != payloadHash) throw new OperationConflictException();
            return receipt.ResourceId;
        }

        var resourceId = await create(cancellationToken);
        if (receipt is null)
        {
            receipt = new OperationReceipt { OwnerId = ownerId, OperationType = operationType, Key = key, PayloadHash = payloadHash };
            database.OperationReceipts.Add(receipt);
        }
        receipt.PayloadHash = payloadHash;
        receipt.ResourceId = resourceId;
        receipt.CreatedAt = clock.GetUtcNow();
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return resourceId;
    }
}
