using Loupe.Application.Common;
using Loupe.Application.Images;
using Microsoft.AspNetCore.Diagnostics;

namespace Loupe.Api.Errors;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var validation = exception as RequestValidationException;
        var status = exception switch
        {
            RequestValidationException => 400,
            ResourceNotFoundException => 404,
            RevisionConflictException => 409,
            ImageValidationException { Failure: ImageFailure.TooLarge } => 413,
            ImageValidationException { Failure: ImageFailure.Unsupported } => 415,
            ImageValidationException => 422,
            _ => 500
        };
        var code = exception switch
        {
            RequestValidationException => "invalid_request",
            ResourceNotFoundException => "item_unavailable",
            RevisionConflictException => "revision_conflict",
            ImageValidationException { Failure: ImageFailure.TooLarge } => "image_too_large",
            ImageValidationException { Failure: ImageFailure.Unsupported } => "unsupported_media",
            ImageValidationException => "invalid_image",
            _ => "unexpected_failure"
        };
        logger.LogWarning("Request {CorrelationId} failed with {Code}", context.TraceIdentifier, code);
        await Results.Problem(statusCode: status, title: status < 500 ? exception.Message : "The request could not be completed.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = context.TraceIdentifier,
                ["errors"] = validation?.Errors
            }).ExecuteAsync(context);
        return true;
    }
}
