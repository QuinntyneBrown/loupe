using Loupe.Domain.Sessions;
namespace Loupe.Application.Sessions;

public interface IJwtService
{
    string Issuer { get; }
    string Create(ApplicationSession session);
    Task<string?> ValidateAsync(string token);
}
