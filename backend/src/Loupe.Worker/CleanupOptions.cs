namespace Loupe.Worker;

public sealed class CleanupOptions
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMinutes(1);
}
