using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Loupe.Api.Tests;

public sealed class CapturedApiFailure : IExceptionHandler
{
    public Exception? Exception { get; private set; }
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        Exception = exception;
        return ValueTask.FromResult(false);
    }
}
