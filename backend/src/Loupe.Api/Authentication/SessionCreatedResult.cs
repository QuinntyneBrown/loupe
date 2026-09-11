using Loupe.Application.Sessions;
using Microsoft.AspNetCore.Mvc;
namespace Loupe.Api.Authentication;

public sealed class SessionCreatedResult(SessionLoginResult result) : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext context)
    {
        context.HttpContext.Response.Cookies.Append("__Host-loupe-session", result.Token, SessionAuthenticationHandler.CookieOptions());
        await new OkObjectResult(result.Session).ExecuteResultAsync(context);
    }
}
