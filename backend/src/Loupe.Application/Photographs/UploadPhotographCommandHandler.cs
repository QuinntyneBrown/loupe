using Loupe.Application.Images;
using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.Security;
using Loupe.Domain.Photographs;
using MediatR;
using System.Security.Cryptography;
using System.Text.Json;

namespace Loupe.Application.Photographs;

public sealed class UploadPhotographCommandHandler(ICurrentOwner owner, IPhotographStore photographs,
    IImageIngestor ingestor, IImageStore images, IOperationReceiptStore operations, TimeProvider clock) : IRequestHandler<UploadPhotographCommand, PhotographResult>
{
    public async Task<PhotographResult> Handle(UploadPhotographCommand request, CancellationToken cancellationToken)
    {
        var key = UploadPhotographCommandValidator.Key(request);
        UploadPhotographCommandValidator.Files(request);
        var title = UploadPhotographCommandValidator.Title(request);
        var brief = CritiqueBriefValidator.Normalize(request.Brief);
        var image = await request.Image.ReadAsync(cancellationToken);
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            title,
            brief,
            image.Sha256,
            ContentType = request.Image.ContentType.ToLowerInvariant()
        })));
        var id = await operations.ExecuteAsync(owner.Id, "photograph-upload", key, fingerprint,
            token => CreateAsync(request, image.Bytes, title, brief, token), cancellationToken);
        return PhotographResult.From(await photographs.FindOwnedAsync(id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException());
    }

    private async Task<Guid> CreateAsync(UploadPhotographCommand request, byte[] bytes, string title, CritiqueBrief brief, CancellationToken cancellationToken)
    {
        var processed = ingestor.Process(bytes, request.Image.ContentType, cancellationToken);
        string? imageKey = null, previewKey = null;
        try
        {
            imageKey = await images.WriteAsync(processed.Image, cancellationToken);
            previewKey = await images.WriteAsync(processed.Preview, cancellationToken);
        }
        catch
        {
            if (imageKey is not null) images.Delete(imageKey);
            if (previewKey is not null) images.Delete(previewKey);
            throw;
        }
        var photograph = new Photograph
        {
            Id = Guid.NewGuid(),
            OwnerId = owner.Id,
            Title = title,
            CreatedAt = clock.GetUtcNow(),
            ImageKey = imageKey,
            Exif = processed.Exif,
            Brief = brief,
            PreviewKey = previewKey,
            Width = processed.Width,
            Height = processed.Height
        };
        // A database commit can succeed despite an unreceived acknowledgment. Keep its files
        // until managed cleanup can prove they are unreferenced.
        await photographs.SaveAsync(photograph, cancellationToken);
        return photograph.Id;
    }
}
