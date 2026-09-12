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
        var now = clock.GetUtcNow();
        var overdueAt = now - TimeSpan.FromHours(24);
        var overdue = await database.Deletions.Where(operation => operation.CompletedAt == null && operation.DeletedAt <= overdueAt)
            .GroupBy(operation => 1).Select(group => new { Count = group.Count(), Oldest = group.Min(operation => operation.DeletedAt) })
            .SingleOrDefaultAsync(cancellationToken);
        if (overdue is not null)
            logger.LogError(new EventId(3201, "cleanup_overdue"),
                "{EventName}: cleanup is overdue for {PendingCount} deletions; oldest age {OldestAgeSeconds}s. See {Runbook}",
                "cleanup_overdue", overdue.Count, (now - overdue.Oldest).TotalSeconds, "docs/operations/cleanup.md");
        var operations = await database.Deletions.FromSqlRaw("""
            SELECT * FROM journal.deletions WHERE "CompletedAt" IS NULL
            ORDER BY "LastAttemptAt" NULLS FIRST, "DeletedAt", "Id" LIMIT 100 FOR UPDATE SKIP LOCKED
            """).ToListAsync(cancellationToken);
        var completed = 0;
        foreach (var operation in operations)
        {
            operation.LastAttemptAt = clock.GetUtcNow();
            foreach (var key in operation.MediaKeys.ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await database.Photographs.AnyAsync(photo => photo.ImageKey == key || photo.PreviewKey == key, cancellationToken)) continue;
                if (await database.References.AnyAsync(reference => reference.ImageKey == key || reference.PreviewKey == key, cancellationToken)) continue;
                if (await database.LocationImages.AnyAsync(image => image.ImageKey == key || image.PreviewKey == key, cancellationToken)) continue;
                try
                {
                    images.Delete(key);
                    operation.MediaKeys = operation.MediaKeys.Where(item => item != key).ToArray();
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(new EventId(3202, "cleanup_media_pending"),
                        "{EventName}: deletion {DeletionId} has pending media cleanup", "cleanup_media_pending", operation.Id);
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
