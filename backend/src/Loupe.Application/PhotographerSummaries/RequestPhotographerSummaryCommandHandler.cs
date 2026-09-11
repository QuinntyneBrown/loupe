using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.Photographers;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.PhotographerSummaries;

public sealed class RequestPhotographerSummaryCommandHandler(ICurrentOwner owner, IPhotographerStore photographers,
    IOperationReceiptStore receipts, IPhotographerSummaryStore summaries, IBackgroundOperationStore operations,
    IPhotographerSummaryConfiguration configuration) : IRequestHandler<RequestPhotographerSummaryCommand, OperationResult>
{
    public async Task<OperationResult> Handle(RequestPhotographerSummaryCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Provide the photographer revision.");
        var key = request.OperationKey is { Length: > 0 and <= 128 } value && value.All(character => character is >= '!' and <= '~')
            ? value : throw new RequestValidationException("operationKey", "Provide an Idempotency-Key of 1 to 128 visible ASCII characters.");
        _ = await photographers.FindOwnedAsync(owner.Id, request.PhotographerId, cancellationToken) ?? throw new ResourceNotFoundException();
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { request.PhotographerId, request.Revision })));
        var id = await receipts.ExecuteAsync(owner.Id, "photographer-summary", key, fingerprint,
            token => summaries.AdmitAsync(request.PhotographerId, owner.Id, request.Revision, configuration.GetIdentity(), token), cancellationToken);
        return OperationResult.From(await operations.FindOwnedAsync(id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException());
    }
}
