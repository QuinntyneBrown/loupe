using Loupe.Application.Security;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;

namespace Loupe.Infrastructure.Security;

public sealed class CurrentOwner(IHttpContextAccessor accessor) : ICurrentOwner
{
    public string Id => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        (accessor.HttpContext?.User.FindFirst("iss")?.Value ?? throw new UnauthorizedAccessException()) + "\n" + Subject)));
    public string Subject => accessor.HttpContext?.User.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException();
    public string Name => accessor.HttpContext?.User.FindFirst("name")?.Value ?? "Photographer";
}
