using Loupe.Application.Sessions;
using Loupe.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class SessionStore(LibraryDbContext database) : ISessionStore
{
    public async Task CreateAsync(ApplicationSession session, string? previousId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (previousId is not null) await RemoveAsync(previousId, cancellationToken);
        database.Sessions.Add(session);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ApplicationSession?> ReadAndTouchAsync(string id, DateTimeOffset now, DateTimeOffset createdAfter,
        DateTimeOffset activeAfter, CancellationToken cancellationToken)
    {
        var updated = await database.Sessions.Where(session => session.Id == id && session.CreatedAt > createdAfter && session.LastSeenAt > activeAfter
            && database.Users.Any(u => u.Id == session.Subject && u.PasswordVersion == session.UserVersion))
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.LastSeenAt,
                session => session.LastSeenAt < now ? now : session.LastSeenAt), cancellationToken);
        return updated == 0 ? null : await database.Sessions.AsNoTracking().SingleOrDefaultAsync(session => session.Id == id, cancellationToken);
    }

    public Task RemoveAsync(string id, CancellationToken cancellationToken) =>
        database.Sessions.Where(session => session.Id == id).ExecuteDeleteAsync(cancellationToken);
}
