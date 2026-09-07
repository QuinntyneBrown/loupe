using System.Security.Cryptography;

namespace Loupe.Application.Images;

public sealed record ImageUpload(Func<Stream> OpenRead, string Filename, string ContentType, long Length)
{
    public async Task<BufferedImage> ReadAsync(CancellationToken cancellationToken)
    {
        if (Length > UploadLimits.Bytes) throw new ImageValidationException(ImageFailure.TooLarge);
        await using var source = OpenRead();
        using var buffer = new MemoryStream();
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var chunk = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > UploadLimits.Bytes) throw new ImageValidationException(ImageFailure.TooLarge);
            buffer.Write(chunk, 0, read);
            digest.AppendData(chunk, 0, read);
        }
        return new BufferedImage(buffer.ToArray(), Convert.ToHexString(digest.GetHashAndReset()));
    }
}
