using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.Security;
using Loupe.Domain.Photographers;
using MediatR;

namespace Loupe.Application.Photographers;

public sealed class SavePhotographerCommandHandler(ICurrentOwner owner, IPhotographerStore photographers, IPortfolioUrlPolicy policy,
    IOperationReceiptStore receipts, TimeProvider clock) : IRequestHandler<SavePhotographerCommand, SavePhotographerResult>
{
    public async Task<SavePhotographerResult> Handle(SavePhotographerCommand request, CancellationToken cancellationToken)
    {
        if (request.OperationKey is not { Length: > 0 and <= 128 } key || !key.All(character => character is >= '!' and <= '~'))
            throw new RequestValidationException("operationKey", "Provide an Idempotency-Key of 1 to 128 visible ASCII characters.");
        var metadata = PhotographerMetadataValidator.Normalize(request.Name, request.PortfolioUrl, request.Summary, request.Notes, request.Tags, policy);
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(metadata)));
        var alreadySaved = true;
        var id = await receipts.ExecuteAsync(owner.Id, "photographer-save", key, fingerprint, async token =>
        {
            var candidate = new Photographer { Id = Guid.NewGuid(), OwnerId = owner.Id, Name = metadata.Name, PortfolioUrl = metadata.PortfolioUrl,
                Summary = metadata.Summary, SummaryProvenance = metadata.Summary is null ? null : "manual", Notes = metadata.Notes, CreatedAt = clock.GetUtcNow() };
            foreach (var tag in metadata.Tags) candidate.Tags.Add(new PhotographerTag { PhotographerId = candidate.Id, OwnerId = owner.Id,
                Name = tag.Name!, NormalizedName = tag.Name!.ToUpperInvariant(), Category = tag.Category, Provenance = "manual" });
            var saved = await photographers.SaveAsync(candidate, token); alreadySaved = saved.Id != candidate.Id; return saved.Id;
        }, cancellationToken);
        return new(PhotographerResult.From(await photographers.FindOwnedAsync(owner.Id, id, cancellationToken) ?? throw new ResourceNotFoundException()), alreadySaved);
    }
}
