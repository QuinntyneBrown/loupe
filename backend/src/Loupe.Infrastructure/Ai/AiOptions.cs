namespace Loupe.Infrastructure.Ai;

public sealed class AiOptions
{
    public string? Mode { get; set; }
    public string Model { get; set; } = "gpt-5.4-mini-2026-03-17";
    public string? ApiKey { get; set; }
    public int MaxConcurrentCalls { get; set; } = 4;
}
