using Loupe.Application.Images;
using Loupe.Application.Security;
using Loupe.Domain.Photographs;
using MediatR;

namespace Loupe.Application.Photographs;

public sealed class UploadPhotographCommandHandler(ICurrentOwner owner, IPhotographStore photographs,
    IImageIngestor ingestor, IImageStore images, TimeProvider clock) : IRequestHandler<UploadPhotographCommand, PhotographResult>
{
    public async Task<PhotographResult> Handle(UploadPhotographCommand request, CancellationToken cancellationToken)
    {
        var title = request.Title?.Trim();
        if (string.IsNullOrEmpty(title)) title = Path.GetFileNameWithoutExtension(request.Image.Filename.Replace('\\', '/')).Trim();
        if (string.IsNullOrEmpty(title)) title = "Untitled photograph";
        var processed = await ingestor.ProcessAsync(request.Image, cancellationToken);
        string? imageKey = null, previewKey = null;
        try
        {
            imageKey = await images.WriteAsync(processed.Image, cancellationToken);
            previewKey = await images.WriteAsync(processed.Preview, cancellationToken);
            var photograph = new Photograph
            {
                Id = Guid.NewGuid(),
                OwnerId = owner.Id,
                Title = title,
                CreatedAt = clock.GetUtcNow(),
                ImageKey = imageKey,
                PreviewKey = previewKey,
                Width = processed.Width,
                Height = processed.Height
            };
            await photographs.SaveAsync(photograph, cancellationToken);
            return PhotographResult.From(photograph);
        }
        catch
        {
            if (imageKey is not null) images.Delete(imageKey);
            if (previewKey is not null) images.Delete(previewKey);
            throw;
        }
    }
}
