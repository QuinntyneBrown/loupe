using Loupe.Application.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace Loupe.Api.Errors;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var validation = exception as RequestValidationException;
        var status = validation is null ? 500 : 400;
        var code = validation is null ? "unexpected_failure" : "invalid_request";
        logger.LogWarning("Request {CorrelationId} failed with {Code}", context.TraceIdentifier, code);
        await Results.Problem(statusCode: status, title: validation?.Message ?? "The request could not be completed.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = context.TraceIdentifier,
                ["errors"] = validation?.Errors
            }).ExecuteAsync(context);
        return true;
    }
}
