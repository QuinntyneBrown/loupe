using System.Security.Cryptography;
using System.Text.Json;
using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.Photographs;
using Loupe.Application.Security;
using Loupe.Domain.Critiques;
using Loupe.Domain.Operations;
using MediatR;

namespace Loupe.Application.Critiques;

public sealed class RetryCritiqueCommandHandler(ICurrentOwner owner, IPhotographStore photographs, IBackgroundOperationStore operations,
    IOperationReceiptStore receipts, ICritiqueConfiguration configuration, TimeProvider clock) : IRequestHandler<RetryCritiqueCommand, OperationResult>
{
    public async Task<OperationResult> Handle(RetryCritiqueCommand request, CancellationToken cancellationToken)
    {
        var key = RetryCritiqueCommandValidator.Validate(request);
        var source = await operations.FindOwnedAsync(request.OperationId, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        _ = await photographs.FindOwnedAsync(source.ResourceId, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { request.OperationId, request.Revision })));
        var id = await receipts.ExecuteAsync(owner.Id, "retry-critique", key, fingerprint, async token =>
        {
            if (source.Type != OperationType.Critique || source.Status != OperationStatus.Failed
                || source.FailureCode is not ("invalid_output" or "provider_timeout" or "worker_interrupted" or "provider_unavailable" or "provider_rate_limited"))
                throw new RetryUnavailableException();
            var now = clock.GetUtcNow();
            if (source.RetryAvailableAt is { } available && available > now) throw new RetryNotReadyException(available - now);
            var photograph = await photographs.FindOwnedAsync(source.ResourceId, owner.Id, token) ?? throw new ResourceNotFoundException();
            if (photograph.Revision != request.Revision) throw new RevisionConflictException();
            var identity = configuration.GetIdentity();
            var original = source.InputJson is null ? null : JsonSerializer.Deserialize<CritiqueInput>(source.InputJson);
            var current = new CritiqueInput(photograph.ImageKey, photograph.Brief, photograph.Exif, photograph.PreviewKey);
            if (identity.Mode != source.Mode || identity.Model != source.Model || identity.PromptVersion != source.PromptVersion
                || original is null || JsonSerializer.Serialize(original) != JsonSerializer.Serialize(current)) throw new AnalysisInputsChangedException();
            return await operations.AdmitCritiqueAsync(source.ResourceId, owner.Id, request.Revision, true, identity, token);
        }, cancellationToken);
        return OperationResult.From(await operations.FindOwnedAsync(id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException());
    }
}
