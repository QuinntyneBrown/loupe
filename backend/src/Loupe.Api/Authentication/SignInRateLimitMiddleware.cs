using System.Globalization;
namespace Loupe.Api.Authentication;

public sealed class SignInRateLimitMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, SignInRateLimiter limiter)
    {
        if (HttpMethods.IsPost(context.Request.Method) && context.Request.Path == "/api/session/sign-in")
        {
            var retry = limiter.Acquire(context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
            if (retry > 0)
            {
                context.Response.Headers.RetryAfter = retry.ToString(CultureInfo.InvariantCulture);
                await Results.Problem(statusCode: 429, title: "Too many sign-in attempts. Try again shortly.",
                    extensions: new Dictionary<string, object?> { ["code"] = "sign_in_limit", ["correlationId"] = context.TraceIdentifier }).ExecuteAsync(context);
                return;
            }
        }
        await next(context);
    }
}
