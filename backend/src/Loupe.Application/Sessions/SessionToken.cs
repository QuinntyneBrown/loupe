using System.Security.Cryptography;
using System.Text;

namespace Loupe.Application.Sessions;

public static class SessionToken
{
    public static string Create() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static string? Hash(string? token) => token is { Length: 64 } && token.All(Uri.IsHexDigit)
        ? Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token))) : null;
}
