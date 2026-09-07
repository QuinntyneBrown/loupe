namespace Loupe.Application.Maintenance;

public interface IAbandonedMediaCleaner
{
    Task<int> CleanAsync(CancellationToken cancellationToken);
}
