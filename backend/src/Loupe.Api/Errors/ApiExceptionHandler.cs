using Loupe.Application.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace Loupe.Api.Errors;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var validation = exception as RequestValidationException;
        var status = exception switch { RequestValidationException => 400, ResourceNotFoundException => 404, _ => 500 };
        var code = exception switch { RequestValidationException => "invalid_request", ResourceNotFoundException => "item_unavailable", _ => "unexpected_failure" };
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
