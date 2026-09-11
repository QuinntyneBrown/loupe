using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Photographers;
using Loupe.Domain.Photographers;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class PhotographerStore(LibraryDbContext database) : IPhotographerStore
{
    public Task<Photographer?> FindOwnedAsync(string ownerId, Guid id, CancellationToken cancellationToken) =>
        database.Photographers.AsNoTracking().Include(item => item.Tags).SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken);

    public async Task<Photographer> SaveAsync(Photographer photographer, CancellationToken cancellationToken)
    {
        if (database.Database.CurrentTransaction is null) throw new InvalidOperationException("Portfolio writes require a transaction.");
        var source = await database.Database.SqlQuery<ReferenceSourceIdentity>($"SELECT loupe_normalize_source({photographer.PortfolioUrl}) AS \"NormalizedSource\", md5(loupe_normalize_source({photographer.PortfolioUrl})) AS \"Hash\"").SingleAsync(cancellationToken);
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { scope = "photographer-source", photographer.OwnerId, source.Hash }));
        var key = BinaryPrimitives.ReadInt64BigEndian(hash);
        await database.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
        var existing = await database.Photographers.FromSqlInterpolated($"""
            SELECT * FROM photographers WHERE "OwnerId" = {photographer.OwnerId} AND "PortfolioHash" = {source.Hash}
            AND loupe_normalize_source("PortfolioUrl") = {source.NormalizedSource}
            """).Include(item => item.Tags).SingleOrDefaultAsync(cancellationToken);
        if (existing is not null) return existing;
        database.Photographers.Add(photographer); await database.SaveChangesAsync(cancellationToken); return photographer;
    }
}
