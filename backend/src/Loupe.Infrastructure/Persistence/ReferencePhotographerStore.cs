using Loupe.Application.Common;
using Loupe.Application.Photographers;
using Loupe.Application.References;
using Loupe.Domain.References;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class ReferencePhotographerStore(LibraryDbContext database) : IReferencePhotographerStore
{
    private IQueryable<Reference> Candidates(string ownerId, string query)
    {
        var items = database.References.AsNoTracking().Where(item => item.OwnerId == ownerId);
        if (query.Length == 0) return items;
        var pattern = "%" + query.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
        return items.Where(item => EF.Functions.ILike(item.Title, pattern, "\\") ||
            EF.Functions.ILike(item.Attribution ?? "", pattern, "\\") ||
            EF.Functions.ILike(item.Description ?? "", pattern, "\\") ||
            EF.Functions.ILike(item.Notes ?? "", pattern, "\\") ||
            EF.Functions.ILike(item.SourceUrl ?? "", pattern, "\\") ||
            item.Tags.Any(tag => EF.Functions.ILike(tag.Name, pattern, "\\")));
    }

    public Task<int> CountCandidatesAsync(string ownerId, string query, CancellationToken cancellationToken) => Candidates(ownerId, query).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<ReferenceCandidate>> CandidatesAsync(string ownerId, string query, int count, CreatedCursor? cursor, CancellationToken cancellationToken)
    {
        var items = Candidates(ownerId, query);
        if (cursor is not null) items = items.Where(item => item.CreatedAt < cursor.CreatedAt || item.CreatedAt == cursor.CreatedAt && item.Id.CompareTo(cursor.Id) > 0);
        return await items.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id).Take(count)
            .Select(item => new ReferenceCandidate(item.Id, item.Title, item.CreatedAt,
                item.PreviewKey == null ? null : $"/api/references/{item.Id}/preview" + (item.ImageRevision > 1 ? $"?v={item.ImageRevision}" : ""),
                item.Revision, item.Photographer == null ? null : new ReferencePhotographerResult(item.Photographer.Id, item.Photographer.Name, item.Photographer.PortfolioUrl)))
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(string ownerId, Guid photographerId, CancellationToken cancellationToken) =>
        database.References.CountAsync(item => item.OwnerId == ownerId && item.PhotographerId == photographerId, cancellationToken);

    public async Task<IReadOnlyList<ReferenceSummary>> ListAsync(string ownerId, Guid photographerId, int count, CreatedCursor? cursor, CancellationToken cancellationToken)
    {
        var query = database.References.AsNoTracking().Where(item => item.OwnerId == ownerId && item.PhotographerId == photographerId);
        if (cursor is not null) query = query.Where(item => item.CreatedAt < cursor.CreatedAt || item.CreatedAt == cursor.CreatedAt && item.Id.CompareTo(cursor.Id) > 0);
        return await query.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id).Take(count)
            .Select(item => new ReferenceSummary(item.Id, item.Title, item.CreatedAt, item.Width, item.Height,
                item.PreviewKey == null ? null : $"/api/references/{item.Id}/preview" + (item.ImageRevision > 1 ? $"?v={item.ImageRevision}" : ""), item.SourceUrl, item.Attribution)).ToListAsync(cancellationToken);
    }

    public async Task<Reference> SetAsync(string ownerId, Guid referenceId, long revision, Guid? photographerId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Lock the selected bookmark before the reference, matching bookmark deletion.
        var photographer = photographerId is { } id
            ? await database.Photographers.FromSqlInterpolated($"SELECT * FROM photographers WHERE \"Id\" = {id} AND \"OwnerId\" = {ownerId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken) ?? throw new ResourceNotFoundException()
            : null;
        var reference = await database.References.FromSqlInterpolated($"SELECT * FROM \"references\" WHERE \"Id\" = {referenceId} AND \"OwnerId\" = {ownerId} FOR UPDATE")
            .Include(item => item.Boards).Include(item => item.Tags).SingleOrDefaultAsync(cancellationToken) ?? throw new ResourceNotFoundException();
        if (reference.Revision != revision) throw new RevisionConflictException();
        if (reference.PhotographerId == photographerId) return reference;
        reference.PhotographerId = photographerId; reference.Photographer = photographer;
        if (photographer is not null && string.IsNullOrWhiteSpace(reference.Attribution)) reference.Attribution = photographer.Name;
        reference.Revision++;
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
        await transaction.CommitAsync(cancellationToken); return reference;
    }
}
