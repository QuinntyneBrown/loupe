using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Images;
using Loupe.Application.Operations;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.References;

public sealed class ReplaceReferenceImageCommandHandler(ICurrentOwner owner, IReferenceStore references,
    IReferenceImageStore replacements, IImageIngestor ingestor, IOperationReceiptStore receipts)
    : IRequestHandler<ReplaceReferenceImageCommand, ReferenceResult>
{
    public async Task<ReferenceResult> Handle(ReplaceReferenceImageCommand request, CancellationToken cancellationToken)
    {
        if (request.FileCount != 1) throw new RequestValidationException("image", "Choose exactly one image.");
        if (request.OperationKey is not { Length: > 0 and <= 128 } key || !key.All(character => character is >= '!' and <= '~'))
            throw new RequestValidationException("operationKey", "Provide an Idempotency-Key of 1 to 128 visible ASCII characters.");
        _ = await references.FindOwnedAsync(request.Id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        var image = await request.Image.ReadAsync(cancellationToken);
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            request.Id, request.Revision, image.Sha256, ContentType = request.Image.ContentType.ToLowerInvariant()
        })));
        await receipts.ExecuteAsync(owner.Id, "reference-image", key, fingerprint, async token =>
        {
            var processed = ingestor.Process(image.Bytes, request.Image.ContentType, token);
            await replacements.ReplaceAsync(request.Id, owner.Id, request.Revision, processed, token);
            return request.Id;
        }, cancellationToken);
        return ReferenceResult.From(await references.FindOwnedAsync(request.Id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException());
    }
}
