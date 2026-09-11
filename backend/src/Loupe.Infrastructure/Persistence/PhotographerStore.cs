using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Photographers;
using Loupe.Application.Common;
using Loupe.Application.References;
using Loupe.Domain.Photographers;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class PhotographerStore(LibraryDbContext database) : IPhotographerStore
{
    public Task<Photographer?> FindSourceOwnedAsync(string ownerId, string url, CancellationToken cancellationToken) =>
        database.Photographers.FromSqlInterpolated($"""
            SELECT * FROM photographers WHERE "OwnerId" = {ownerId} AND "PortfolioHash" = md5(loupe_normalize_source({url}))
            AND loupe_normalize_source("PortfolioUrl") = loupe_normalize_source({url})
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
    public Task<int> CountAsync(string ownerId, CancellationToken cancellationToken, string query = "") =>
        Matching(ownerId, query).CountAsync(cancellationToken);

    private IQueryable<Photographer> Matching(string ownerId, string query)
    {
        var items = database.Photographers.AsNoTracking().Where(item => item.OwnerId == ownerId);
        if (query.Length == 0) return items;
        var pattern = "%" + query.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
        return items.Where(item => EF.Functions.ILike(item.Name, pattern, "\\") ||
            EF.Functions.ILike(item.PortfolioUrl, pattern, "\\") || EF.Functions.ILike(item.Summary ?? "", pattern, "\\") ||
            EF.Functions.ILike(item.Notes ?? "", pattern, "\\") || item.Tags.Any(tag => EF.Functions.ILike(tag.Name, pattern, "\\")));
    }

    public async Task<IReadOnlyList<PhotographerSummary>> ListAsync(string ownerId, int count, CreatedCursor? cursor, CancellationToken cancellationToken, string search = "")
    {
        var query = Matching(ownerId, search);
        if (cursor is not null) query = query.Where(item => item.CreatedAt < cursor.CreatedAt || item.CreatedAt == cursor.CreatedAt && item.Id.CompareTo(cursor.Id) > 0);
        return await query.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id).Take(count)
            .Select(item => new PhotographerSummary(item.Id, item.Name, item.PortfolioUrl, item.CreatedAt, item.Summary,
                item.Tags.OrderBy(tag => tag.Name).Select(tag => new PhotographerTagResult(tag.Name, tag.Category, tag.Provenance)).ToArray(),
                database.References.Count(reference => reference.OwnerId == ownerId && reference.PhotographerId == item.Id),
                database.References.Where(reference => reference.OwnerId == ownerId && reference.PhotographerId == item.Id)
                    .OrderByDescending(reference => reference.CreatedAt).ThenBy(reference => reference.Id).Take(3)
                    .Select(reference => new ReferenceSummary(reference.Id, reference.Title, reference.CreatedAt, reference.Width, reference.Height,
                        reference.PreviewKey == null ? null : $"/api/references/{reference.Id}/preview" + (reference.ImageRevision > 1 ? $"?v={reference.ImageRevision}" : ""), reference.SourceUrl, reference.Attribution)).ToArray()))
            .ToListAsync(cancellationToken);
    }

    public Task<Photographer?> FindOwnedAsync(string ownerId, Guid id, CancellationToken cancellationToken) =>
        database.Photographers.AsNoTracking().Include(item => item.Tags).SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken);

    public async Task<Photographer> SaveAsync(Photographer photographer, CancellationToken cancellationToken)
    {
        if (database.Database.CurrentTransaction is null) throw new InvalidOperationException("Portfolio writes require a transaction.");
        var source = await LockPortfolioAsync(photographer.OwnerId, photographer.PortfolioUrl, cancellationToken);
        var existing = await database.Photographers.FromSqlInterpolated($"""
            SELECT * FROM photographers WHERE "OwnerId" = {photographer.OwnerId} AND "PortfolioHash" = {source.Hash}
            AND loupe_normalize_source("PortfolioUrl") = {source.NormalizedSource}
            """).Include(item => item.Tags).SingleOrDefaultAsync(cancellationToken);
        if (existing is not null) return existing;
        database.Photographers.Add(photographer); await database.SaveChangesAsync(cancellationToken); return photographer;
    }

    public async Task<Photographer> UpdateAsync(string ownerId, Guid id, long revision, PhotographerMetadata metadata, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var source = await LockPortfolioAsync(ownerId, metadata.PortfolioUrl, cancellationToken);
        var photographer = await database.Photographers.FromSqlInterpolated($"SELECT * FROM photographers WHERE \"Id\" = {id} AND \"OwnerId\" = {ownerId} FOR UPDATE")
            .Include(item => item.Tags).SingleOrDefaultAsync(cancellationToken) ?? throw new ResourceNotFoundException();
        if (photographer.Revision != revision) throw new RevisionConflictException();
        var duplicate = await database.Photographers.FromSqlInterpolated($"""
            SELECT * FROM photographers WHERE "OwnerId" = {ownerId} AND "Id" <> {id} AND "PortfolioHash" = {source.Hash}
            AND loupe_normalize_source("PortfolioUrl") = {source.NormalizedSource}
            """).AnyAsync(cancellationToken);
        if (duplicate) throw new PortfolioConflictException();
        var previousSource = await database.Database.SqlQuery<string>($"SELECT loupe_normalize_source({photographer.PortfolioUrl}) AS \"Value\"").SingleAsync(cancellationToken);
        if (previousSource != source.NormalizedSource) { photographer.SourceRevision++; photographer.SourceFailureCode = null; }
        photographer.Name = metadata.Name; photographer.PortfolioUrl = metadata.PortfolioUrl;
        if (photographer.Summary != metadata.Summary) photographer.SummaryProvenance = metadata.Summary is null ? null : "manual";
        photographer.Summary = metadata.Summary; photographer.Notes = metadata.Notes;
        var names = metadata.Tags.Select(tag => tag.Name!.ToUpperInvariant()).ToHashSet();
        foreach (var tag in photographer.Tags.Where(tag => !names.Contains(tag.NormalizedName)).ToArray()) photographer.Tags.Remove(tag);
        foreach (var input in metadata.Tags)
        {
            var name = input.Name!.ToUpperInvariant();
            var tag = photographer.Tags.SingleOrDefault(tag => tag.NormalizedName == name);
            if (tag is null) photographer.Tags.Add(new PhotographerTag { PhotographerId = id, OwnerId = ownerId, NormalizedName = name, Name = input.Name, Category = input.Category, Provenance = "manual" });
            else if (tag.Name != input.Name || tag.Category != input.Category) { tag.Name = input.Name; tag.Category = input.Category; tag.Provenance = "manual"; }
        }
        photographer.Revision++;
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
        await transaction.CommitAsync(cancellationToken); return photographer;
    }

    private async Task<ReferenceSourceIdentity> LockPortfolioAsync(string ownerId, string url, CancellationToken cancellationToken)
    {
        var source = await database.Database.SqlQuery<ReferenceSourceIdentity>($"SELECT loupe_normalize_source({url}) AS \"NormalizedSource\", md5(loupe_normalize_source({url})) AS \"Hash\"").SingleAsync(cancellationToken);
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { scope = "photographer-source", OwnerId = ownerId, source.Hash }));
        var key = BinaryPrimitives.ReadInt64BigEndian(hash);
        await database.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
        return source;
    }
}
