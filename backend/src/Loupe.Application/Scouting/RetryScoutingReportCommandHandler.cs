using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Locations;
using Loupe.Application.Operations;
using Loupe.Application.Security;
using Loupe.Domain.Operations;
using Loupe.Domain.Scouting;
using MediatR;

namespace Loupe.Application.Scouting;

public sealed class RetryScoutingReportCommandHandler(ICurrentOwner owner, ILocationStore locations, IBackgroundOperationStore operations,
    IOperationReceiptStore receipts, IScoutingStore scouting, IScoutingConfiguration configuration, TimeProvider clock)
    : IRequestHandler<RetryScoutingReportCommand, OperationResult>
{
    public async Task<OperationResult> Handle(RetryScoutingReportCommand request, CancellationToken cancellationToken)
    {
        var source = await operations.FindOwnedAsync(request.OperationId, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        _ = await locations.FindOwnedAsync(source.ResourceId, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { request.OperationId, request.Revision })));
        var id = await receipts.ExecuteAsync(owner.Id, "retry-scouting", request.OperationKey, fingerprint, async token =>
        {
            if (source.Type != OperationType.LocationScouting || source.Status != OperationStatus.Failed
                || source.FailureCode is not ("invalid_output" or "provider_timeout" or "worker_interrupted" or "provider_unavailable" or "provider_rate_limited"))
                throw new RetryUnavailableException();
            var now = clock.GetUtcNow();
            if (source.RetryAvailableAt is { } available && available > now) throw new RetryNotReadyException(available - now);
            var location = await locations.FindOwnedAsync(source.ResourceId, owner.Id, token) ?? throw new ResourceNotFoundException();
            if (location.Revision != request.Revision) throw new RevisionConflictException();
            var identity = configuration.GetIdentity();
            var original = source.InputJson is null ? null : JsonSerializer.Deserialize<ScoutingInput>(source.InputJson);
            if (identity.Mode != source.Mode || identity.Model != source.Model || identity.PromptVersion != source.PromptVersion
                || original is null || JsonSerializer.Serialize(original) != JsonSerializer.Serialize(ScoutingInputBuilder.From(location)))
                throw new AnalysisInputsChangedException();
            return await scouting.AdmitAsync(source.ResourceId, owner.Id, request.Revision, true, identity, token);
        }, cancellationToken);
        return OperationResult.From(await operations.FindOwnedAsync(id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException());
    }
}
