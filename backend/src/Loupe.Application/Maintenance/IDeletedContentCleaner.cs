namespace Loupe.Application.Maintenance;

public interface IDeletedContentCleaner
{
    Task<int> CleanAsync(CancellationToken cancellationToken);
}
