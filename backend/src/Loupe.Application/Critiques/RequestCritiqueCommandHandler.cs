using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.Photographs;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Critiques;

public sealed class RequestCritiqueCommandHandler(ICurrentOwner owner, IPhotographStore photographs,
    IOperationReceiptStore receipts, IBackgroundOperationStore operations, ICritiqueConfiguration configuration)
    : IRequestHandler<RequestCritiqueCommand, OperationResult>
{
    public async Task<OperationResult> Handle(RequestCritiqueCommand request, CancellationToken cancellationToken)
    {
        var key = RequestCritiqueCommandValidator.Validate(request);
        _ = await photographs.FindOwnedAsync(request.PhotographId, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        var identity = configuration.GetIdentity();
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { request.PhotographId, request.Revision, request.Regenerate })));
        var id = await receipts.ExecuteAsync(owner.Id, "critique", key, fingerprint,
            token => operations.AdmitCritiqueAsync(request.PhotographId, owner.Id, request.Revision, identity, token), cancellationToken);
        return OperationResult.From(await operations.FindOwnedAsync(id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException());
    }
}
