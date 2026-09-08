using Loupe.Application.References;
using Loupe.Domain.References;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class ReferenceStore(LibraryDbContext database) : IReferenceStore
{
    public async Task SaveAsync(Reference reference, CancellationToken cancellationToken)
    {
        database.References.Add(reference);
        await database.SaveChangesAsync(cancellationToken);
    }
    public Task<Reference?> FindOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
        database.References.AsNoTracking().SingleOrDefaultAsync(reference => reference.Id == id && reference.OwnerId == ownerId, cancellationToken);
}
