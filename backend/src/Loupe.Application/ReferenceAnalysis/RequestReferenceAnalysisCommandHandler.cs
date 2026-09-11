using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.References;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.ReferenceAnalysis;

public sealed class RequestReferenceAnalysisCommandHandler(ICurrentOwner owner, IReferenceStore references,
    IOperationReceiptStore receipts, IReferenceAnalysisStore analyses, IBackgroundOperationStore operations,
    IReferenceAnalysisConfiguration configuration) : IRequestHandler<RequestReferenceAnalysisCommand, OperationResult>
{
    public async Task<OperationResult> Handle(RequestReferenceAnalysisCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Provide the reference revision.");
        var key = request.OperationKey is { Length: > 0 and <= 128 } value && value.All(character => character is >= '!' and <= '~')
            ? value : throw new RequestValidationException("operationKey", "Provide an Idempotency-Key of 1 to 128 visible ASCII characters.");
        _ = await references.FindOwnedAsync(request.ReferenceId, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { request.ReferenceId, request.Revision })));
        var id = await receipts.ExecuteAsync(owner.Id, "reference-analysis", key, fingerprint,
            token => analyses.AdmitAsync(request.ReferenceId, owner.Id, request.Revision, configuration.GetIdentity(), token), cancellationToken);
        return OperationResult.From(await operations.FindOwnedAsync(id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException());
    }
}
