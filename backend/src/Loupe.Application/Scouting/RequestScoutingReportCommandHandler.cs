using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Locations;
using Loupe.Application.Operations;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Scouting;

public sealed class RequestScoutingReportCommandHandler(ICurrentOwner owner, ILocationStore locations, IOperationReceiptStore receipts,
    IScoutingStore scouting, IBackgroundOperationStore operations, IScoutingConfiguration configuration)
    : IRequestHandler<RequestScoutingReportCommand, OperationResult>
{
    public async Task<OperationResult> Handle(RequestScoutingReportCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Provide the location revision.");
        var key = request.OperationKey is { Length: > 0 and <= 128 } value && value.All(character => character is >= '!' and <= '~')
            ? value : throw new RequestValidationException("operationKey", "Provide an Idempotency-Key of 1 to 128 visible ASCII characters.");
        var identity = configuration.GetIdentity();
        _ = await locations.FindOwnedAsync(request.LocationId, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { request.LocationId, request.Revision, request.Regenerate })));
        var id = await receipts.ExecuteAsync(owner.Id, "scouting", key, fingerprint,
            token => scouting.AdmitAsync(request.LocationId, owner.Id, request.Revision, request.Regenerate, identity, token), cancellationToken);
        return OperationResult.From(await operations.FindOwnedAsync(id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException());
    }
}
