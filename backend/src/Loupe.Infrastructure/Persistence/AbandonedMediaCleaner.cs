using Loupe.Application.Maintenance;
using Loupe.Infrastructure.Images;
using Loupe.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loupe.Infrastructure.Persistence;

public sealed class AbandonedMediaCleaner(LibraryDbContext database, IOptions<MediaOptions> options, TimeProvider clock,
    ILogger<AbandonedMediaCleaner> logger) : IAbandonedMediaCleaner
{
    public async Task<int> CleanAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await MediaTransactionLock.ProtectCleanupAsync(database, cancellationToken);
        var now = clock.GetUtcNow();
        var expired = database.ReferenceDrafts.Where(draft => draft.ExpiresAt <= now).Select(draft => draft.Id);
        await database.BackgroundOperations.Where(operation => operation.Type == OperationType.ReferenceDraftImport && expired.Contains(operation.ResourceId))
            .ExecuteUpdateAsync(setters => setters.SetProperty(operation => operation.Status, OperationStatus.Canceled)
                .SetProperty(operation => operation.InputJson, (string?)null).SetProperty(operation => operation.OutputJson, (string?)null)
                .SetProperty(operation => operation.CompletedAt, now).SetProperty(operation => operation.UpdatedAt, now)
                .SetProperty(operation => operation.LeaseToken, (Guid?)null).SetProperty(operation => operation.LeaseExpiresAt, (DateTimeOffset?)null)
                .SetProperty(operation => operation.NextAttemptAt, (DateTimeOffset?)null).SetProperty(operation => operation.Message, "Preview expired."), cancellationToken);
        await database.ReferenceDrafts.Where(draft => draft.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);
        if (!Directory.Exists(options.Value.Root)) { await transaction.CommitAsync(cancellationToken); return 0; }
        var keys = await database.Photographs.AsNoTracking().Select(photo => new { photo.ImageKey, photo.PreviewKey }).ToListAsync(cancellationToken);
        var referenced = keys.SelectMany(photo => new[] { photo.ImageKey, photo.PreviewKey }).ToHashSet(StringComparer.Ordinal);
        var referenceKeys = await database.References.AsNoTracking().Select(reference => new { reference.ImageKey, reference.PreviewKey }).ToListAsync(cancellationToken);
        referenced.UnionWith(referenceKeys.SelectMany(reference => new[] { reference.ImageKey, reference.PreviewKey }).OfType<string>());
        var draftKeys = await database.ReferenceDrafts.AsNoTracking().Where(draft => draft.ExpiresAt > clock.GetUtcNow())
            .Select(draft => new { draft.ImageKey, draft.PreviewKey }).ToListAsync(cancellationToken);
        referenced.UnionWith(draftKeys.SelectMany(draft => new[] { draft.ImageKey, draft.PreviewKey }).OfType<string>());
        var cutoff = (clock.GetUtcNow() - TimeSpan.FromHours(1)).UtcDateTime;
        var removed = 0;
        var failed = 0;
        foreach (var path in Directory.EnumerateFiles(options.Value.Root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(path);
            var key = name.EndsWith(".partial", StringComparison.Ordinal) ? name[..^8] : name;
            if (key.Length != 32 || !key.All(Uri.IsHexDigit) || referenced.Contains(name)) continue;
            try
            {
                var file = new FileInfo(path);
                if ((file.Attributes & FileAttributes.ReparsePoint) != 0 || file.LastWriteTimeUtc > cutoff) continue;
                file.Delete();
                if (++removed == 100) break;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { failed++; }
        }
        if (failed > 0) logger.LogWarning("Abandoned media cleanup has {FailedCount} pending files", failed);
        await transaction.CommitAsync(cancellationToken);
        return removed;
    }
}
