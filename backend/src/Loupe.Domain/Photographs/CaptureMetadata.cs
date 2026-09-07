namespace Loupe.Domain.Photographs;

public sealed class CaptureMetadata
{
    public string? Camera { get; init; }
    public string? Lens { get; init; }
    public string? Aperture { get; init; }
    public string? ShutterSpeed { get; init; }
    public string? Iso { get; init; }
    public string? FocalLength { get; init; }
    public string? CapturedAt { get; init; }
}
