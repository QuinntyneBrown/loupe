namespace Loupe.Application.Operations;

public sealed class RetryUnavailableException() : Exception("This operation cannot be retried. Review its status and integration configuration before requesting new work.");
