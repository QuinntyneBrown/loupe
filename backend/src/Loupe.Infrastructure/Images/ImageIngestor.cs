using Loupe.Application.Images;
using NetVips;

namespace Loupe.Infrastructure.Images;

public sealed class ImageIngestor : IImageIngestor
{
    public async Task<ProcessedImage> ProcessAsync(ImageUpload upload, CancellationToken cancellationToken)
    {
        await using var source = upload.OpenRead();
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        using var decoded = Image.NewFromBuffer(buffer.ToArray(), failOn: Enums.FailOn.Error);
        using var oriented = decoded.Autorot();
        var image = oriented.PngsaveBuffer(keep: Enums.ForeignKeep.None);
        using var thumbnail = oriented.ThumbnailImage(1600, height: 1600, size: Enums.Size.Down);
        return new ProcessedImage(image, thumbnail.JpegsaveBuffer(q: 85, keep: Enums.ForeignKeep.None), oriented.Width, oriented.Height);
    }
}
