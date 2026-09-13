namespace Loupe.Worker;

public sealed class IndexingOptions
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(15);
}
