using System.Buffers.Binary;
using Loupe.Application.Images;

namespace Loupe.Infrastructure.Images;

public static class ImageSignature
{
    public static string? ContentType(byte[] bytes)
    {
        var data = bytes.AsSpan();
        if (data.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (data.StartsWith(new byte[] { 255, 216, 255 })) return "image/jpeg";
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8)) return "image/webp";
        if (data.Length >= 8 && data[4..8].SequenceEqual("ftyp"u8) && IsHeic(data)) return "image/heic";
        return null;
    }

    private static bool IsHeic(ReadOnlySpan<byte> data)
    {
        ulong length = BinaryPrimitives.ReadUInt32BigEndian(data);
        var header = 8;
        if (length == 1)
        {
            if (data.Length < 16) throw new ImageValidationException(ImageFailure.Invalid);
            length = BinaryPrimitives.ReadUInt64BigEndian(data[8..]);
            header = 16;
        }
        if (length < (ulong)(header + 8) || length > (ulong)data.Length || (length - (ulong)header) % 4 != 0)
            throw new ImageValidationException(ImageFailure.Invalid);
        var major = data.Slice(header, 4);
        if (IsHeicBrand(major)) return true;
        if (!major.SequenceEqual("mif1"u8)) return false;
        for (var offset = header + 8; offset < (int)length; offset += 4)
            if (IsHeicBrand(data.Slice(offset, 4))) return true;
        return false;
    }

    private static bool IsHeicBrand(ReadOnlySpan<byte> brand) => brand.SequenceEqual("heic"u8) || brand.SequenceEqual("heix"u8);
}
