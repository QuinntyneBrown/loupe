using Loupe.Application.References;
using Loupe.Application.Common;
using Loupe.Domain.References;
using Microsoft.EntityFrameworkCore;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Loupe.Infrastructure.Persistence;

public sealed class ReferenceStore(LibraryDbContext database) : IReferenceStore
{
    public Task<Reference?> FindSourceOwnedAsync(string ownerId, string source, CancellationToken cancellationToken) =>
        database.References.FromSqlInterpolated($"""
            SELECT * FROM "references" WHERE "OwnerId" = {ownerId} AND "SourceHash" = md5(loupe_normalize_source({source}))
              AND loupe_normalize_source("SourceUrl") = loupe_normalize_source({source})
            """).AsNoTracking().Include(item => item.Boards).Include(item => item.Tags)
            .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id).FirstOrDefaultAsync(cancellationToken);
    public async Task<Reference> SaveSourceAsync(Reference reference, CancellationToken cancellationToken)
    {
        var source = await LockSourceAsync(reference.OwnerId, reference.SourceUrl, cancellationToken)
            ?? throw new InvalidOperationException("A source save requires a source URL.");
        var existing = await database.References.FromSqlInterpolated($"""
            SELECT * FROM "references" WHERE "OwnerId" = {reference.OwnerId} AND "SourceHash" = {source.Hash}
              AND loupe_normalize_source("SourceUrl") = {source.NormalizedSource}
            """).Include(item => item.Boards).Include(item => item.Tags).OrderBy(item => item.CreatedAt).ThenBy(item => item.Id).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null) return existing;
        database.References.Add(reference);
        await database.SaveChangesAsync(cancellationToken);
        return reference;
    }

    private async Task<ReferenceSourceIdentity?> LockSourceAsync(string ownerId, string? sourceUrl, CancellationToken cancellationToken)
    {
        if (sourceUrl is null) return null;
        if (database.Database.CurrentTransaction is null) throw new InvalidOperationException("Source writes require a transaction.");
        var source = await database.Database.SqlQuery<ReferenceSourceIdentity>($"""
            SELECT loupe_normalize_source({sourceUrl}) AS "NormalizedSource", md5(loupe_normalize_source({sourceUrl})) AS "Hash"
            """).SingleAsync(cancellationToken);
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { scope = "reference-source", ownerId, source.Hash }));
        var key = BinaryPrimitives.ReadInt64BigEndian(hash);
        await database.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
        return source;
    }

    public async Task<Reference> UpdateAsync(Guid id, string ownerId, long revision, ReferenceMetadata metadata, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await LockSourceAsync(ownerId, metadata.SourceUrl, cancellationToken);
        var reference = await database.References.Include(item => item.Boards).Include(item => item.Tags).SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken)
            ?? throw new ResourceNotFoundException();
        if (reference.Revision != revision) throw new RevisionConflictException();
        reference.Title = metadata.Title;
        reference.SourceUrl = metadata.SourceUrl;
        reference.Attribution = metadata.Attribution;
        reference.Notes = metadata.Notes;
        reference.Revision++;
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
        await transaction.CommitAsync(cancellationToken);
        return reference;
    }

    private IQueryable<Reference> Filtered(string ownerId, Guid? boardId, string[] tags)
    {
        var query = database.References.AsNoTracking()
            .Where(reference => reference.OwnerId == ownerId && (boardId == null || reference.Boards.Any(item => item.BoardId == boardId)));
        foreach (var tag in tags) query = query.Where(reference => reference.Tags.Any(item => item.NormalizedName == tag));
        return query;
    }

    public Task<int> CountAsync(string ownerId, Guid? boardId, string[] tags, CancellationToken cancellationToken) => Filtered(ownerId, boardId, tags).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<ReferenceSummary>> ListAsync(string ownerId, int count, CreatedCursor? cursor, Guid? boardId, string[] tags, CancellationToken cancellationToken)
    {
        var query = Filtered(ownerId, boardId, tags);
        if (cursor is not null) query = query.Where(reference => reference.CreatedAt < cursor.CreatedAt
            || reference.CreatedAt == cursor.CreatedAt && reference.Id.CompareTo(cursor.Id) > 0);
        return await query.OrderByDescending(reference => reference.CreatedAt).ThenBy(reference => reference.Id).Take(count)
            .Select(reference => new ReferenceSummary(reference.Id, reference.Title, reference.CreatedAt,
                reference.Width, reference.Height, reference.PreviewKey == null ? null : $"/api/references/{reference.Id}/preview",
                reference.SourceUrl, reference.Attribution))
            .ToListAsync(cancellationToken);
    }
    public async Task SaveAsync(Reference reference, CancellationToken cancellationToken)
    {
        await LockSourceAsync(reference.OwnerId, reference.SourceUrl, cancellationToken);
        database.References.Add(reference);
        await database.SaveChangesAsync(cancellationToken);
    }
    public Task<Reference?> FindOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
        database.References.AsNoTracking().Include(item => item.Boards).Include(item => item.Tags).SingleOrDefaultAsync(reference => reference.Id == id && reference.OwnerId == ownerId, cancellationToken);
}
