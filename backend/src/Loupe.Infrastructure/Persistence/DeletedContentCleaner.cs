using Loupe.Application.Images;
using Loupe.Application.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Loupe.Infrastructure.Persistence;

public sealed class DeletedContentCleaner(LibraryDbContext database, IImageStore images, TimeProvider clock,
    ILogger<DeletedContentCleaner> logger) : IDeletedContentCleaner
{
    public async Task<int> CleanAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var operations = await database.Deletions.FromSqlRaw("""
            SELECT * FROM journal.deletions WHERE "CompletedAt" IS NULL
            ORDER BY "DeletedAt", "Id" LIMIT 100 FOR UPDATE SKIP LOCKED
            """).ToListAsync(cancellationToken);
        var completed = 0;
        foreach (var operation in operations)
        {
            foreach (var key in operation.MediaKeys.ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await database.Photographs.AnyAsync(photo => photo.ImageKey == key || photo.PreviewKey == key, cancellationToken)) continue;
                try
                {
                    images.Delete(key);
                    operation.MediaKeys = operation.MediaKeys.Where(item => item != key).ToArray();
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning("Deletion {DeletionId} has pending media cleanup", operation.Id);
                }
            }
            if (operation.MediaKeys.Length == 0)
            {
                operation.CompletedAt = clock.GetUtcNow();
                completed++;
            }
        }
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return completed;
    }
}
