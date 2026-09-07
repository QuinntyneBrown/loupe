using Loupe.Domain.Sessions;

namespace Loupe.Application.Sessions;

public interface ISessionStore
{
    Task CreateAsync(ApplicationSession session, string? previousId, CancellationToken cancellationToken);
    Task<ApplicationSession?> ReadAndTouchAsync(string id, DateTimeOffset now, DateTimeOffset createdAfter,
        DateTimeOffset activeAfter, CancellationToken cancellationToken);
    Task RemoveAsync(string id, CancellationToken cancellationToken);
}
