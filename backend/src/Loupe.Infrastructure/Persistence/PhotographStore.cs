using Loupe.Application.Photographs;
using Loupe.Domain.Photographs;
using Loupe.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Loupe.Application.Common;

namespace Loupe.Infrastructure.Persistence;

public sealed class PhotographStore(LibraryDbContext database) : IPhotographStore
{
    public async Task SaveAsync(Photograph photograph, CancellationToken cancellationToken)
    {
        database.Photographs.Add(photograph);
        await database.SaveChangesAsync(cancellationToken);
    }

    public Task<Photograph?> FindOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
        database.Photographs.AsNoTracking().SingleOrDefaultAsync(photograph => photograph.Id == id && photograph.OwnerId == ownerId, cancellationToken);

    public async Task<IReadOnlyList<PhotographSummary>> ListAsync(string ownerId, int count, CreatedCursor? cursor, CancellationToken cancellationToken)
    {
        var query = database.Photographs.AsNoTracking().Where(photograph => photograph.OwnerId == ownerId);
        if (cursor is not null)
            query = query.Where(photograph => photograph.CreatedAt < cursor.CreatedAt
                || photograph.CreatedAt == cursor.CreatedAt && photograph.Id.CompareTo(cursor.Id) > 0);
        return await query.OrderByDescending(photograph => photograph.CreatedAt).ThenBy(photograph => photograph.Id).Take(count)
            .Select(photograph => new PhotographSummary(photograph.Id, photograph.Title, photograph.CreatedAt,
                photograph.Width, photograph.Height, $"/api/photographs/{photograph.Id}/preview", photograph.CritiqueJson != null,
                database.BackgroundOperations.Where(operation => operation.Id == photograph.CurrentCritiqueOperationId
                    && operation.OwnerId == ownerId && operation.ResourceId == photograph.Id && operation.Type == OperationType.Critique)
                    .Select(operation => (OperationStatus?)operation.Status).FirstOrDefault())).ToListAsync(cancellationToken);
    }

    public Task<Photograph> UpdateBriefAsync(Guid id, string ownerId, long revision, CritiqueBrief brief, CancellationToken cancellationToken) =>
        UpdateOwnedAsync(id, ownerId, revision, photograph => photograph.Brief = brief, cancellationToken);

    public Task<Photograph> UpdateNotesAsync(Guid id, string ownerId, long revision, string? notes, CancellationToken cancellationToken) =>
        UpdateOwnedAsync(id, ownerId, revision, photograph => photograph.Notes = notes, cancellationToken);

    private async Task<Photograph> UpdateOwnedAsync(Guid id, string ownerId, long revision, Action<Photograph> change, CancellationToken cancellationToken)
    {
        var photograph = await database.Photographs.SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken)
            ?? throw new ResourceNotFoundException();
        if (photograph.Revision != revision) throw new RevisionConflictException();
        change(photograph);
        photograph.Revision++;
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
        return photograph;
    }
}
