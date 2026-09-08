namespace Loupe.Application.Operations;

public sealed class ProviderFailureException(ProviderFailureKind kind, TimeSpan? retryAfter = null) : Exception("The provider request failed.")
{
    public ProviderFailureKind Kind { get; } = kind;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
