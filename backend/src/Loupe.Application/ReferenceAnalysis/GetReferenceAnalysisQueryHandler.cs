using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.References;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.ReferenceAnalysis;

public sealed class GetReferenceAnalysisQueryHandler(ICurrentOwner owner, IReferenceStore references,
    IBackgroundOperationStore operations) : IRequestHandler<GetReferenceAnalysisQuery, OperationResult?>
{
    public async Task<OperationResult?> Handle(GetReferenceAnalysisQuery request, CancellationToken cancellationToken)
    {
        var reference = await references.FindOwnedAsync(request.ReferenceId, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        if (reference.CurrentAnalysisOperationId is not { } id) return null;
        return OperationResult.From(await operations.FindOwnedAsync(id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException());
    }
}
