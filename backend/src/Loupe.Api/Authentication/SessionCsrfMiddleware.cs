using Microsoft.AspNetCore.Antiforgery;

namespace Loupe.Api.Authentication;

public sealed class SessionCsrfMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (context.User.Identity?.IsAuthenticated == true && HttpMethods.IsGet(context.Request.Method)
            && context.Request.Path == "/api/session")
            context.Response.Headers["X-CSRF-Token"] = antiforgery.GetAndStoreTokens(context).RequestToken;
        await next(context);
    }
}
