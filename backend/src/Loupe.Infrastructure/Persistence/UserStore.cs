using Loupe.Application.Users;
using Loupe.Application.Common;
using Loupe.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Loupe.Infrastructure.Persistence;

public sealed class UserStore(LibraryDbContext database) : IUserStore
{
    public Task<User?> FindAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        database.Users.AsNoTracking().SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
    public async Task CreateAsync(User user, CancellationToken cancellationToken)
    {
        database.Users.Add(user);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new RequestValidationException("email", "An account with this email already exists."); }
    }
    public async Task ResetPasswordAsync(string normalizedEmail, string hash, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var version = Guid.NewGuid().ToString("N");
        var updated = await database.Users.Where(u => u.NormalizedEmail == normalizedEmail)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.PasswordHash, hash).SetProperty(u => u.PasswordVersion, version), cancellationToken);
        if (updated == 0) throw new RequestValidationException("email", "The account does not exist.");
        var id = await database.Users.Where(u => u.NormalizedEmail == normalizedEmail).Select(u => u.Id).SingleAsync(cancellationToken);
        await database.Sessions.Where(s => s.Subject == id).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
