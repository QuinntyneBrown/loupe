using Loupe.Application.Photographs;
using Loupe.Domain.Photographs;
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

    public async Task<Photograph> UpdateBriefAsync(Guid id, string ownerId, long revision, CritiqueBrief brief, CancellationToken cancellationToken)
    {
        var photograph = await database.Photographs.SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken)
            ?? throw new ResourceNotFoundException();
        if (photograph.Revision != revision) throw new RevisionConflictException();
        photograph.Brief = brief;
        photograph.Revision++;
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new RevisionConflictException(); }
        return photograph;
    }
}
