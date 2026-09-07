namespace Loupe.Application.Images;

public interface IImageIngestor
{
    ProcessedImage Process(byte[] bytes, string declaredContentType, CancellationToken cancellationToken);
}
