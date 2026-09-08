using Loupe.Application.References;
using Loupe.Application.Common;
using Loupe.Domain.References;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class ReferenceStore(LibraryDbContext database) : IReferenceStore
{
    public async Task<Reference> UpdateAsync(Guid id, string ownerId, long revision, ReferenceMetadata metadata, CancellationToken cancellationToken)
    {
        var reference = await database.References.SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken)
            ?? throw new ResourceNotFoundException();
        if (reference.Revision != revision) throw new RevisionConflictException();
        reference.Title = metadata.Title;
        reference.SourceUrl = metadata.SourceUrl;
        reference.Attribution = metadata.Attribution;
        reference.Notes = metadata.Notes;
        reference.Revision++;
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
        return reference;
    }

    public async Task<IReadOnlyList<ReferenceSummary>> ListAsync(string ownerId, int count, CreatedCursor? cursor, CancellationToken cancellationToken)
    {
        var query = database.References.AsNoTracking().Where(reference => reference.OwnerId == ownerId);
        if (cursor is not null) query = query.Where(reference => reference.CreatedAt < cursor.CreatedAt
            || reference.CreatedAt == cursor.CreatedAt && reference.Id.CompareTo(cursor.Id) > 0);
        return await query.OrderByDescending(reference => reference.CreatedAt).ThenBy(reference => reference.Id).Take(count)
            .Select(reference => new ReferenceSummary(reference.Id, reference.Title, reference.CreatedAt,
                reference.Width, reference.Height, reference.PreviewKey == null ? null : $"/api/references/{reference.Id}/preview"))
            .ToListAsync(cancellationToken);
    }
    public async Task SaveAsync(Reference reference, CancellationToken cancellationToken)
    {
        database.References.Add(reference);
        await database.SaveChangesAsync(cancellationToken);
    }
    public Task<Reference?> FindOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
        database.References.AsNoTracking().SingleOrDefaultAsync(reference => reference.Id == id && reference.OwnerId == ownerId, cancellationToken);
}
