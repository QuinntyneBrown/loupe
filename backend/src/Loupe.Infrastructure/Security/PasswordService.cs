using Loupe.Application.Users;
using Microsoft.AspNetCore.Identity;
namespace Loupe.Infrastructure.Security;

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<object> hasher = new();
    private readonly object user = new();
    private readonly string dummy;
    public PasswordService() => dummy = hasher.HashPassword(user, Guid.NewGuid().ToString());
    public string Hash(string password) => hasher.HashPassword(user, password);
    public bool Verify(string? hash, string password)
    {
        var result = hasher.VerifyHashedPassword(user, hash ?? dummy, password);
        return hash is not null && result != PasswordVerificationResult.Failed;
    }
}
