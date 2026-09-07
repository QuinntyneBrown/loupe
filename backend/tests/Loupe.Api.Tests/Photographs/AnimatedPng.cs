using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Loupe.Api.Tests.Photographs;

// Synthetic two-frame RGB APNG, following the PNG specification's acTL/fcTL/fdAT sequence.
public static class AnimatedPng
{
    public static byte[] Create()
    {
        using var output = new MemoryStream();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        byte[] header = [.. UInt32(2), .. UInt32(2), 8, 2, 0, 0, 0];
        WriteChunk(output, "IHDR", header);
        WriteChunk(output, "acTL", [.. UInt32(2), .. UInt32(0)]);
        WriteChunk(output, "fcTL", FrameControl(0));
        WriteChunk(output, "IDAT", Frame(0));
        WriteChunk(output, "fcTL", FrameControl(1));
        WriteChunk(output, "fdAT", [.. UInt32(2), .. Frame(255)]);
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static byte[] FrameControl(uint sequence) =>
        [.. UInt32(sequence), .. UInt32(2), .. UInt32(2), .. UInt32(0), .. UInt32(0), 0, 1, 0, 10, 0, 0];

    private static byte[] Frame(byte color)
    {
        using var output = new MemoryStream();
        using (var compressed = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            for (var row = 0; row < 2; row++) compressed.Write([0, color, color, color, color, color, color]);
        return output.ToArray();
    }

    private static byte[] UInt32(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }

    private static void WriteChunk(Stream output, string type, byte[] payload)
    {
        output.Write(UInt32((uint)payload.Length));
        byte[] body = [.. Encoding.ASCII.GetBytes(type), .. payload];
        output.Write(body);
        var crc = uint.MaxValue;
        foreach (var value in body)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320);
        }
        output.Write(UInt32(~crc));
    }
}
