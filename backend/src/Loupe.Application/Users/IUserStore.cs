using Loupe.Domain.Users;
namespace Loupe.Application.Users;

public interface IUserStore
{
    Task<User?> FindAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task CreateAsync(User user, CancellationToken cancellationToken);
    Task ResetPasswordAsync(string normalizedEmail, string hash, CancellationToken cancellationToken);
}
