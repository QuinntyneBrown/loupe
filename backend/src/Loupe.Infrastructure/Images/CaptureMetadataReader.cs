using Loupe.Domain.Photographs;
using NetVips;

namespace Loupe.Infrastructure.Images;

public static class CaptureMetadataReader
{
    public static CaptureMetadata Read(Image image) => new()
    {
        Camera = Value(image, "exif-ifd0-Model") ?? Value(image, "exif-ifd0-Make"),
        Lens = Value(image, "exif-ifd2-LensModel"),
        Aperture = Value(image, "exif-ifd2-FNumber"),
        ShutterSpeed = Value(image, "exif-ifd2-ExposureTime"),
        Iso = Value(image, "exif-ifd2-ISOSpeedRatings"),
        FocalLength = Value(image, "exif-ifd2-FocalLength"),
        CapturedAt = Value(image, "exif-ifd2-DateTimeOriginal")
    };

    private static string? Value(Image image, string field)
    {
        if (image.GetTypeOf(field) == 0 || image.Get(field) is not string text) return null;
        var description = text.LastIndexOf(" (", StringComparison.Ordinal);
        var value = (description < 0 ? text : text[..description]).Trim();
        return value.Length == 0 ? null : value;
    }
}
