using Loupe.Domain.Photographs;

namespace Loupe.Domain.Critiques;

public sealed record CritiqueInput(string ImageKey, CritiqueBrief Brief, CaptureMetadata Exif, string PreviewKey);
