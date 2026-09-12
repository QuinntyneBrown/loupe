using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Locations;
using Loupe.Application.Operations;
using Loupe.Application.Security;
using Loupe.Domain.Operations;
using MediatR;

namespace Loupe.Application.ShootPlanning;

/// <summary>Re-queues a failed index run: the document is re-read at run time, so no input comparison applies.</summary>
public sealed class RetryLocationIndexCommandHandler(ICurrentOwner owner, ILocationStore locations, IBackgroundOperationStore operations,
    IOperationReceiptStore receipts, ILocationIndexWorkStore work) : IRequestHandler<RetryLocationIndexCommand, OperationResult>
{
    public async Task<OperationResult> Handle(RetryLocationIndexCommand request, CancellationToken cancellationToken)
    {
        var source = await operations.FindOwnedAsync(request.OperationId, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        _ = await locations.FindOwnedAsync(source.ResourceId, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { request.OperationId, request.Revision })));
        var id = await receipts.ExecuteAsync(owner.Id, "retry-index", request.OperationKey, fingerprint, async token =>
        {
            if (source.Type != OperationType.LocationIndex || source.Status != OperationStatus.Failed) throw new RetryUnavailableException();
            var location = await locations.FindOwnedAsync(source.ResourceId, owner.Id, token) ?? throw new ResourceNotFoundException();
            if (location.Revision != request.Revision) throw new RevisionConflictException();
            return await work.RequeueAsync(source.ResourceId, owner.Id, token);
        }, cancellationToken);
        return OperationResult.From(await operations.FindOwnedAsync(id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException());
    }
}
