using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.Photographers;
using Loupe.Application.ReferenceDrafts;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.PhotographerDrafts;

public sealed class SavePhotographerDraftCommandHandler(ICurrentOwner owner, IPhotographerDraftStore drafts, IPhotographerStore photographers,
    IPortfolioUrlPolicy policy, IOperationReceiptStore receipts) : IRequestHandler<SavePhotographerDraftCommand, SavePhotographerResult>
{
    public async Task<SavePhotographerResult> Handle(SavePhotographerDraftCommand request, CancellationToken cancellationToken)
    {
        var key = DraftOperationKey.Validate(request.OperationKey);
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the preview revision you opened.");
        var metadata = PhotographerMetadataValidator.Normalize(request.Name, request.PortfolioUrl, request.Summary, request.Notes, request.Tags, policy);
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { request.Id, request.Revision, metadata })));
        var alreadySaved = true;
        var id = await receipts.ExecuteAsync(owner.Id, "photographer-draft-save", key, fingerprint, async token =>
        {
            var result = await drafts.CommitAsync(owner.Id, request.Id, request.Revision, metadata, token);
            alreadySaved = result.AlreadySaved; return result.Photographer.Id;
        }, cancellationToken);
        return new(PhotographerResult.From(await photographers.FindOwnedAsync(owner.Id, id, cancellationToken) ?? throw new ResourceNotFoundException()), alreadySaved);
    }
}
