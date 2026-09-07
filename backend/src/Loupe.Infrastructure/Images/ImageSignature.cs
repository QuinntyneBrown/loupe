using System.Text;

namespace Loupe.Infrastructure.Images;

public static class ImageSignature
{
    public static string? ContentType(byte[] bytes)
    {
        var data = bytes.AsSpan();
        if (data.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (data.StartsWith(new byte[] { 255, 216, 255 })) return "image/jpeg";
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8)) return "image/webp";
        if (data.Length >= 12 && data[4..8].SequenceEqual("ftyp"u8) && Encoding.ASCII.GetString(data[8..12]) is "heic" or "heix") return "image/heic";
        return null;
    }
}
