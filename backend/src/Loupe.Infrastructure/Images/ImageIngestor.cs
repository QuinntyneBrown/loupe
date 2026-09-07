using Loupe.Application.Images;
using NetVips;

namespace Loupe.Infrastructure.Images;

public sealed class ImageIngestor : IImageIngestor
{
    public ProcessedImage Process(byte[] bytes, string declaredContentType, CancellationToken cancellationToken)
    {
        var contentType = ImageSignature.ContentType(bytes);
        if (contentType is null || !string.Equals(contentType, declaredContentType, StringComparison.OrdinalIgnoreCase))
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
