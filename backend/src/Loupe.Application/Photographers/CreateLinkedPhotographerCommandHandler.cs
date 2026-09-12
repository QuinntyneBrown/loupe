using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.References;
using Loupe.Application.Security;
using Loupe.Domain.Photographers;
using MediatR;

namespace Loupe.Application.Photographers;

public sealed class CreateLinkedPhotographerCommandHandler(ICurrentOwner owner, IPhotographerStore photographers,
    IReferencePhotographerStore links, IReferenceStore references, IPortfolioUrlPolicy policy,
    IOperationReceiptStore receipts, TimeProvider clock) : IRequestHandler<CreateLinkedPhotographerCommand, ReferenceResult>
{
    public async Task<ReferenceResult> Handle(CreateLinkedPhotographerCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the reference revision you opened.");
        if (request.OperationKey is not { Length: > 0 and <= 128 } key || !key.All(character => character is >= '!' and <= '~'))
            throw new RequestValidationException("operationKey", "Provide an Idempotency-Key of 1 to 128 visible ASCII characters.");
        var metadata = PhotographerMetadataValidator.Normalize(request.Name, request.PortfolioUrl, null, null, null, policy);
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { request.ReferenceId, request.Revision, metadata.Name, metadata.PortfolioUrl })));
        var id = await receipts.ExecuteAsync(owner.Id, "reference-create-photographer", key, fingerprint, async token =>
        {
            var candidate = new Photographer { Id = Guid.NewGuid(), OwnerId = owner.Id, Name = metadata.Name,
                PortfolioUrl = metadata.PortfolioUrl, CreatedAt = clock.GetUtcNow() };
            var photographer = await photographers.SaveAsync(candidate, token);
            // The receipt transaction owns both writes: a stale or missing reference rolls back creation.
            await links.SetAsync(owner.Id, request.ReferenceId, request.Revision, photographer.Id, token);
            return request.ReferenceId;
        }, cancellationToken);
        return ReferenceResult.From(await references.FindOwnedAsync(id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException());
    }
}
