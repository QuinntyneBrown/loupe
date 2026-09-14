using Loupe.Domain.Photographs;

namespace Loupe.Domain.Scouting;

public sealed record ScoutingImageInput(Guid ImageId, int Position, string PreviewKey, CaptureMetadata Exif);
