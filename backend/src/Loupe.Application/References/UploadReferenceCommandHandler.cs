using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Images;
using Loupe.Application.Operations;
using Loupe.Application.Security;
using Loupe.Domain.References;
using MediatR;

namespace Loupe.Application.References;

public sealed class UploadReferenceCommandHandler(ICurrentOwner owner, IReferenceStore references,
    IImageIngestor ingestor, IImageStore images, IOperationReceiptStore operations, TimeProvider clock)
    : IRequestHandler<UploadReferenceCommand, ReferenceResult>
{
    public async Task<ReferenceResult> Handle(UploadReferenceCommand request, CancellationToken cancellationToken)
    {
        var key = UploadReferenceCommandValidator.Key(request);
        var metadata = UploadReferenceCommandValidator.Normalize(request);
        var image = await request.Image.ReadAsync(cancellationToken);
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            metadata,
            image.Sha256,
            ContentType = request.Image.ContentType.ToLowerInvariant()
        })));
        var id = await operations.ExecuteAsync(owner.Id, "reference-upload", key, fingerprint,
            token => CreateAsync(request, image.Bytes, metadata, token), cancellationToken);
        return ReferenceResult.From(await references.FindOwnedAsync(id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException());
    }
    private async Task<Guid> CreateAsync(UploadReferenceCommand request, byte[] bytes, ReferenceMetadata metadata, CancellationToken cancellationToken)
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
        var reference = new Reference
        {
            Id = Guid.NewGuid(),
            OwnerId = owner.Id,
            Title = metadata.Title,
            CreatedAt = clock.GetUtcNow(),
            SourceUrl = metadata.SourceUrl,
            Attribution = metadata.Attribution,
            Notes = metadata.Notes,
            ImageKey = imageKey,
            PreviewKey = previewKey,
            Width = processed.Width,
            Height = processed.Height
        };
        // Preserve staged files after an uncertain commit; cleanup verifies live references.
        await references.SaveAsync(reference, cancellationToken);
        return reference.Id;
    }
}
