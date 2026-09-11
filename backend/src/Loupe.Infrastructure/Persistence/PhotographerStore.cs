using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Photographers;
using Loupe.Application.Common;
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
