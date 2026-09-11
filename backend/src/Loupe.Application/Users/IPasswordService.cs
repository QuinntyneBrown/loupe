namespace Loupe.Application.Users;

public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string? hash, string password);
}
