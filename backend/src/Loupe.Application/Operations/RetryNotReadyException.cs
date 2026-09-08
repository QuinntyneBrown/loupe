namespace Loupe.Application.Operations;

public sealed class RetryNotReadyException(TimeSpan wait) : Exception("The provider's retry time has not arrived.")
{
    public TimeSpan Wait { get; } = wait;
}
