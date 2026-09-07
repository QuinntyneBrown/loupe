using Loupe.Application.Images;

namespace Loupe.Api.Uploads;

public sealed class UploadBodyLimitMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.ContentType?.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase) != true)
        {
            await next(context);
            return;
        }
        if (context.Request.ContentLength > UploadLimits.RequestBytes) throw new ImageValidationException(ImageFailure.TooLarge);
        var original = context.Request.Body;
        using var limited = new UploadBodyStream(original);
        context.Request.Body = limited;
        try { await next(context); }
        finally { context.Request.Body = original; }
    }
}
