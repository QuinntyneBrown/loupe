using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;

namespace Loupe.Api.Authentication;

public sealed class SessionCsrfMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery, IOptions<BrowserOptions> options)
    {
        if (context.Request.Path.StartsWithSegments("/api") && context.User.Identity?.IsAuthenticated == true
            && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)
            && !HttpMethods.IsOptions(context.Request.Method))
        {
            var origin = context.Request.Headers.Origin;
            var trusted = origin.Count == 1 && options.Value.AllowedOrigins.Contains(origin[0], StringComparer.Ordinal);
            if (!trusted || !context.Request.Headers.ContainsKey("X-CSRF-Token") || !await antiforgery.IsRequestValidAsync(context))
            {
                await Results.Problem(statusCode: 403, title: "The request protection could not be verified.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "request_protection_failed",
                        ["correlationId"] = context.TraceIdentifier
                    }).ExecuteAsync(context);
                return;
            }
        }
        if (context.User.Identity?.IsAuthenticated == true && HttpMethods.IsGet(context.Request.Method)
            && context.Request.Path == "/api/session")
            context.Response.Headers["X-CSRF-Token"] = antiforgery.GetAndStoreTokens(context).RequestToken;
        await next(context);
    }
}
