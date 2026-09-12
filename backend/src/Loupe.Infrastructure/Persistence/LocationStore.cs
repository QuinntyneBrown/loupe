using Loupe.Application.Locations;
using Loupe.Domain.Locations;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class LocationStore(LibraryDbContext context) : ILocationStore
{
    public async Task SaveAsync(Location location, CancellationToken cancellationToken)
    {
        context.Locations.Add(location);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<Location?> FindOwnedAsync(Guid id, string ownerId, CancellationToken cancellationToken) =>
        context.Locations.AsNoTracking().Include(location => location.Tags)
            .SingleOrDefaultAsync(location => location.Id == id && location.OwnerId == ownerId, cancellationToken);
}
