using Loupe.Domain.Photographs;

namespace Loupe.Domain.Locations;

public sealed class LocationImage
{
    public required Guid Id { get; init; }
    public required Guid LocationId { get; init; }
    public required string OwnerId { get; init; }
    public required int Position { get; set; }
    public required string ImageKey { get; init; }
    public required string PreviewKey { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required CaptureMetadata Exif { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
