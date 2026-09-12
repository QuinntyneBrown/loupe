using Loupe.Application.Common;
using Loupe.Application.Photographers;
using Loupe.Application.Boards;
using Loupe.Application.Images;
using Loupe.Application.Operations;
using Microsoft.AspNetCore.Diagnostics;
using System.Globalization;

namespace Loupe.Api.Errors;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var validation = exception as RequestValidationException;
        var status = exception switch
        {
            Loupe.Application.Sessions.InvalidCredentialsException => 401,
            RequestValidationException => 400,
            BoardNameConflictException => 409,
            PortfolioConflictException => 409,
            ResourceNotFoundException => 404,
            RevisionConflictException => 409,
            OperationConflictException => 409,
            AnalysisActiveException => 409,
            AnalysisLimitException => 429,
            AnalysisInputsChangedException => 409,
            RetryUnavailableException => 409,
            RetryNotReadyException => 429,
            ServiceUnavailableException => 503,
            SearchUnavailableException => 503,
            SearchRefreshRequiredException => 409,
            IntegrationNotConfiguredException => 503,
            ImageValidationException { Failure: ImageFailure.TooLarge } => 413,
            ImageValidationException { Failure: ImageFailure.Unsupported } => 415,
            ImageValidationException => 422,
            _ => 500
        };
        var code = exception switch
        {
            Loupe.Application.Sessions.InvalidCredentialsException => "invalid_credentials",
            RequestValidationException => "invalid_request",
            BoardNameConflictException => "board_name_conflict",
            PortfolioConflictException => "portfolio_conflict",
            ResourceNotFoundException => "item_unavailable",
            RevisionConflictException => "revision_conflict",
            OperationConflictException => "operation_conflict",
            AnalysisActiveException => "analysis_active",
            AnalysisLimitException => "analysis_limit",
            AnalysisInputsChangedException => "analysis_inputs_changed",
            RetryUnavailableException => "retry_unavailable",
            RetryNotReadyException => "retry_not_ready",
            ServiceUnavailableException => "service_unavailable",
            SearchUnavailableException => "search_unavailable",
            SearchRefreshRequiredException => "refresh_required",
            IntegrationNotConfiguredException => "integration_not_configured",
            ImageValidationException { Failure: ImageFailure.TooLarge } => "image_too_large",
            ImageValidationException { Failure: ImageFailure.Unsupported } => "unsupported_media",
            ImageValidationException => "invalid_image",
            _ => "unexpected_failure"
        };
        logger.LogWarning("Request {CorrelationId} failed with {Code}", context.TraceIdentifier, code);
        if (exception is ServiceUnavailableException or IntegrationNotConfiguredException or SearchUnavailableException) context.Response.Headers.RetryAfter = "5";
        if (exception is AnalysisLimitException) context.Response.Headers.RetryAfter = "30";
        if (exception is RetryNotReadyException notReady)
            context.Response.Headers.RetryAfter = Math.Ceiling(notReady.Wait.TotalSeconds).ToString("0", CultureInfo.InvariantCulture);
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
