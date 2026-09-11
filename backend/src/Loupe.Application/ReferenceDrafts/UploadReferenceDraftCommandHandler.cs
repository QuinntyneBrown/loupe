using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Images;
using Loupe.Application.Operations;
using Loupe.Application.References;
using Loupe.Application.Security;
using Loupe.Domain.References;
using MediatR;

namespace Loupe.Application.ReferenceDrafts;

public sealed class UploadReferenceDraftCommandHandler(ICurrentOwner owner, IReferenceDraftStore drafts, IImageIngestor ingestor,
    IImageStore images, IOperationReceiptStore receipts, TimeProvider clock) : IRequestHandler<UploadReferenceDraftCommand, ReferenceDraftResult>
{
    public async Task<ReferenceDraftResult> Handle(UploadReferenceDraftCommand request, CancellationToken cancellationToken)
    {
        if (request.FileCount != 1) throw new RequestValidationException("image", "Choose exactly one image.");
        var key = DraftOperationKey.Validate(request.OperationKey);
        var source = ReferenceMetadataValidator.Source(request.SourceUrl);
        var title = TextField.Default(Path.GetFileNameWithoutExtension(request.Image.Filename.Replace('\\', '/')), 200, "Untitled reference");
        var buffer = await request.Image.ReadAsync(cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { source, title, buffer.Sha256, request.Image.ContentType })));
        var id = await receipts.ExecuteAsync(owner.Id, "reference-draft-upload", key, hash, async token =>
        {
            var processed = ingestor.Process(buffer.Bytes, request.Image.ContentType, token);
            string? imageKey = null, previewKey = null;
            try { imageKey = await images.WriteAsync(processed.Image, token); previewKey = await images.WriteAsync(processed.Preview, token); }
            catch { if (imageKey is not null) images.Delete(imageKey); if (previewKey is not null) images.Delete(previewKey); throw; }
            var draft = new ReferenceDraft
            {
                Id = Guid.NewGuid(),
                OwnerId = owner.Id,
                Title = title,
                SourceUrl = source,
                ImageKey = imageKey,
                PreviewKey = previewKey,
                Width = processed.Width,
                Height = processed.Height,
                ExpiresAt = clock.GetUtcNow().AddHours(24)
            };
            await drafts.AddAsync(draft, token);
            return draft.Id;
        }, cancellationToken);
        return ReferenceDraftResult.From(await drafts.FindOwnedAsync(owner.Id, id, cancellationToken));
    }
}
