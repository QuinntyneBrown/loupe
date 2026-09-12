using Loupe.Application.Images;

namespace Loupe.Application.References;

public interface IReferenceImageStore
{
    Task ReplaceAsync(Guid id, string ownerId, long revision, ProcessedImage image, CancellationToken cancellationToken);
}
