using Loupe.Application.Security;
using Microsoft.AspNetCore.Http;

namespace Loupe.Infrastructure.Security;

public sealed class CurrentOwner(IHttpContextAccessor accessor) : ICurrentOwner
{
    public string Subject => accessor.HttpContext?.User.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException();
    public string Name => accessor.HttpContext?.User.FindFirst("name")?.Value ?? "Photographer";
}
