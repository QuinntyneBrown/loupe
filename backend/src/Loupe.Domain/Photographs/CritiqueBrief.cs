namespace Loupe.Domain.Photographs;

public sealed class CritiqueBrief
{
    public string? Intent { get; init; }
    public string? Genre { get; init; }
    public ExperienceLevel? Experience { get; init; }
    public string? RequestedFeedback { get; init; }
}
