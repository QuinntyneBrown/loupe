namespace Loupe.Application.Maintenance;

public interface IDeletionRetention
{
    Task<int> PruneAsync(CancellationToken cancellationToken);
}
