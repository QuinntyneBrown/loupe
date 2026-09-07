using Loupe.Application.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class DeletionRetention(LibraryDbContext database, TimeProvider clock) : IDeletionRetention
{
    public Task<int> PruneAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow() - TimeSpan.FromDays(35);
        return database.Deletions.Where(operation => operation.CompletedAt != null && operation.DeletedAt <= cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
