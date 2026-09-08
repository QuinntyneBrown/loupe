using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.References;
using Loupe.Application.Security;
using MediatR;
namespace Loupe.Application.ReferenceImports;

public sealed class RequestReferenceImportCommandHandler(ICurrentOwner owner, IReferenceStore references,
    IOperationReceiptStore receipts, IReferenceImportStore imports, IBackgroundOperationStore operations,
    IReferenceImportConfiguration configuration) : IRequestHandler<RequestReferenceImportCommand, OperationResult>
{
    public async Task<OperationResult> Handle(RequestReferenceImportCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Provide the reference revision.");
        var key = request.OperationKey is { Length: > 0 and <= 128 } value && value.All(character => character is >= '!' and <= '~')
            ? value : throw new RequestValidationException("operationKey", "Provide an Idempotency-Key of 1 to 128 visible ASCII characters.");
        _ = await references.FindOwnedAsync(request.ReferenceId, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { request.ReferenceId, request.Revision })));
        var id = await receipts.ExecuteAsync(owner.Id, "reference-import", key, fingerprint,
            token => imports.AdmitAsync(request.ReferenceId, owner.Id, request.Revision, configuration.GetMode(), token), cancellationToken);
        return OperationResult.From(await operations.FindOwnedAsync(id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException());
    }
}
