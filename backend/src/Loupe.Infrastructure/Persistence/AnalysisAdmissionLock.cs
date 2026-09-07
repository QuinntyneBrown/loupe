using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public static class AnalysisAdmissionLock
{
    public static Task AcquireAsync(LibraryDbContext database, string ownerId, CancellationToken cancellationToken)
    {
        var key = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"analysis-admission:{ownerId}")));
        return database.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
    }
}
