namespace Loupe.Application.Images;

public interface IImageIngestor
{
    Task<ProcessedImage> ProcessAsync(ImageUpload upload, CancellationToken cancellationToken);
}
