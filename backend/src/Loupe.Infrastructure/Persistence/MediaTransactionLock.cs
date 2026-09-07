using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public static class MediaTransactionLock
{
    private const long Key = 0x4C4F5550454D4544;
    public static Task ProtectUploadAsync(LibraryDbContext database, CancellationToken cancellationToken) =>
        database.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock_shared({Key})", cancellationToken);
    public static Task ProtectCleanupAsync(LibraryDbContext database, CancellationToken cancellationToken) =>
        database.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({Key})", cancellationToken);
}
