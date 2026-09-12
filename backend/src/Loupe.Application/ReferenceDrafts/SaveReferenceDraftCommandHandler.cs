using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.References;
using Loupe.Application.Security;
using MediatR;
namespace Loupe.Application.ReferenceDrafts;

public sealed class SaveReferenceDraftCommandHandler(ICurrentOwner owner, IReferenceDraftStore drafts, IReferenceStore references,
    IOperationReceiptStore receipts) : IRequestHandler<SaveReferenceDraftCommand, SaveReferenceUrlResult>
{
    public async Task<SaveReferenceUrlResult> Handle(SaveReferenceDraftCommand request, CancellationToken cancellationToken)
    {
        var key = DraftOperationKey.Validate(request.OperationKey);
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the preview revision you opened.");
        if (request.BoardIds is null) throw new RequestValidationException("boardIds", "Supply the selected boards, or an empty list.");
        var metadata = ReferenceMetadataValidator.Normalize(request.Title, request.SourceUrl, request.Attribution, request.Notes);
        var boardIds = request.BoardIds.Distinct().Order().ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { request.Id, request.Revision, metadata, boardIds })));
        var alreadySaved = true;
        var id = await receipts.ExecuteAsync(owner.Id, "reference-draft-save", key, hash, async token =>
        {
            var result = await drafts.CommitAsync(owner.Id, request.Id, request.Revision, metadata, boardIds, token);
            alreadySaved = result.AlreadySaved;
            return result.Reference.Id;
        }, cancellationToken);
        return new SaveReferenceUrlResult(ReferenceResult.From(await references.FindOwnedAsync(id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException()), alreadySaved);
    }
}
