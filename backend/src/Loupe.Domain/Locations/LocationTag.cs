namespace Loupe.Domain.Locations;

public sealed class LocationTag
{
    public required Guid LocationId { get; init; }
    public required string OwnerId { get; init; }
    public required string NormalizedName { get; init; }
    public required string Name { get; set; }
    public string? Category { get; set; }
}
