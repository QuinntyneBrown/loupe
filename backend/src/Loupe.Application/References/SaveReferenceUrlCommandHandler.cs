using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.Security;
using Loupe.Domain.References;
using MediatR;

namespace Loupe.Application.References;

public sealed class SaveReferenceUrlCommandHandler(ICurrentOwner owner, IReferenceStore references,
    IOperationReceiptStore receipts, TimeProvider clock) : IRequestHandler<SaveReferenceUrlCommand, SaveReferenceUrlResult>
{
    public async Task<SaveReferenceUrlResult> Handle(SaveReferenceUrlCommand request, CancellationToken cancellationToken)
    {
        var key = request.OperationKey is { Length: > 0 and <= 128 } value && value.All(character => character is >= '!' and <= '~')
            ? value : throw new RequestValidationException("operationKey", "Provide an Idempotency-Key of 1 to 128 visible ASCII characters.");
        var source = ReferenceMetadataValidator.Source(request.SourceUrl)
            ?? throw new RequestValidationException("sourceUrl", "Enter a source URL.");
        var title = TextField.Normalize(request.Title, 200, "title") ?? TextField.Default(new Uri(source).Host, 200, "Untitled reference");
        var metadata = ReferenceMetadataValidator.Normalize(title, source, request.Attribution, request.Notes);
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(metadata)));
        var alreadySaved = true;
        var id = await receipts.ExecuteAsync(owner.Id, "reference-url", key, fingerprint, async token =>
        {
            var candidate = new Reference
            {
                Id = Guid.NewGuid(),
                OwnerId = owner.Id,
                CreatedAt = clock.GetUtcNow(),
                Title = metadata.Title,
                SourceUrl = source,
                Attribution = metadata.Attribution,
                Notes = metadata.Notes
            };
            var saved = await references.SaveSourceAsync(candidate, token);
            alreadySaved = saved.Id != candidate.Id;
            return saved.Id;
        }, cancellationToken);
        var reference = await references.FindOwnedAsync(id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        return new SaveReferenceUrlResult(ReferenceResult.From(reference), alreadySaved);
    }
}
