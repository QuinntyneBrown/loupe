namespace Loupe.Domain.Videos;

public sealed class VideoTag
{
    public required Guid VideoId { get; init; }
    public required string OwnerId { get; init; }
    public required string NormalizedName { get; init; }
    public required string Name { get; set; }
    public string? Category { get; set; }
    public required string Provenance { get; set; }
}
