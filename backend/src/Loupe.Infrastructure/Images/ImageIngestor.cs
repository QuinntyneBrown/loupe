using Loupe.Application.Images;
using NetVips;

namespace Loupe.Infrastructure.Images;

public sealed class ImageIngestor : IImageIngestor
{
    public async Task<ProcessedImage> ProcessAsync(ImageUpload upload, CancellationToken cancellationToken)
    {
        if (upload.Length > UploadLimits.Bytes) throw new ImageValidationException(ImageFailure.TooLarge);
        await using var source = upload.OpenRead();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > UploadLimits.Bytes) throw new ImageValidationException(ImageFailure.TooLarge);
            buffer.Write(chunk, 0, read);
        }
        var bytes = buffer.ToArray();
        var contentType = ImageSignature.ContentType(bytes);
        if (contentType is null || !string.Equals(contentType, upload.ContentType, StringComparison.OrdinalIgnoreCase))
            throw new ImageValidationException(ImageFailure.Unsupported);
        if (contentType == "image/png") PngContainerValidator.Validate(bytes);
        try
        {
            using var decoded = Image.NewFromBuffer(bytes, failOn: Enums.FailOn.Error);
            if (decoded.GetTypeOf("n-pages") != 0 && (int)decoded.Get("n-pages") != 1)
                throw new ImageValidationException(ImageFailure.Unsupported);
            if (decoded.Width > UploadLimits.Edge || decoded.Height > UploadLimits.Edge || (long)decoded.Width * decoded.Height > UploadLimits.Pixels)
                throw new ImageValidationException(ImageFailure.Invalid);
            using var oriented = decoded.Autorot();
            var image = oriented.PngsaveBuffer(keep: Enums.ForeignKeep.None);
            using var thumbnail = oriented.ThumbnailImage(1600, height: 1600, size: Enums.Size.Down);
            cancellationToken.ThrowIfCancellationRequested();
            return new ProcessedImage(image, thumbnail.JpegsaveBuffer(q: 85, keep: Enums.ForeignKeep.None), oriented.Width, oriented.Height, CaptureMetadataReader.Read(decoded));
        }
        catch (VipsException) { throw new ImageValidationException(ImageFailure.Invalid); }
    }
}
