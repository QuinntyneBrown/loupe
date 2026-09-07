using Loupe.Application.Maintenance;
using Loupe.Infrastructure.Images;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loupe.Infrastructure.Persistence;

public sealed class AbandonedMediaCleaner(LibraryDbContext database, IOptions<MediaOptions> options, TimeProvider clock,
    ILogger<AbandonedMediaCleaner> logger) : IAbandonedMediaCleaner
{
    public async Task<int> CleanAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(options.Value.Root)) return 0;
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await MediaTransactionLock.ProtectCleanupAsync(database, cancellationToken);
        var keys = await database.Photographs.AsNoTracking().Select(photo => new { photo.ImageKey, photo.PreviewKey }).ToListAsync(cancellationToken);
        var referenced = keys.SelectMany(photo => new[] { photo.ImageKey, photo.PreviewKey }).ToHashSet(StringComparer.Ordinal);
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
