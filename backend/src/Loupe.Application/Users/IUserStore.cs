using Loupe.Domain.Users;
namespace Loupe.Application.Users;

public interface IUserStore
{
    Task<User?> FindAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<bool> TryCreateAsync(User user, CancellationToken cancellationToken);
    Task ResetPasswordAsync(string normalizedEmail, string hash, CancellationToken cancellationToken);
}
