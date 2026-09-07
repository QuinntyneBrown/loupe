using Loupe.Domain.Photographs;

namespace Loupe.Application.Images;

public sealed record ProcessedImage(byte[] Image, byte[] Preview, int Width, int Height, CaptureMetadata Exif);
