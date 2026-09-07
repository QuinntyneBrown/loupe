using Loupe.Application.Photographs;
using Loupe.Domain.Photographs;
using Microsoft.EntityFrameworkCore;

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
}
