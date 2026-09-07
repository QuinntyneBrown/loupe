using System.Buffers.Binary;
using Loupe.Application.Images;

namespace Loupe.Infrastructure.Images;

public static class PngContainerValidator
{
    public static void Validate(ReadOnlySpan<byte> bytes)
    {
        var offset = 8;
        while (offset <= bytes.Length - 12)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4));
            if (length > bytes.Length - offset - 12) throw new ImageValidationException(ImageFailure.Invalid);
            var type = bytes.Slice(offset + 4, 4);
            if (type.SequenceEqual("acTL"u8))
                throw new ImageValidationException(length == 8 ? ImageFailure.Unsupported : ImageFailure.Invalid);
            if (type.SequenceEqual("IEND"u8)) return;
            offset += checked((int)length + 12);
        }
        throw new ImageValidationException(ImageFailure.Invalid);
    }
}
