using System.Security.Cryptography;
using System.Text;

namespace Loupe.Application.Sessions;

public static class SessionToken
{
    public static string? Hash(string? token) => token is { Length: > 0 and <= 4096 }
        ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))) : null;
}
