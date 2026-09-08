using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.Photographs;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Critiques;

public sealed class GetCurrentCritiqueOperationQueryHandler(ICurrentOwner owner, IPhotographStore photographs,
    IBackgroundOperationStore operations) : IRequestHandler<GetCurrentCritiqueOperationQuery, OperationResult?>
{
    public async Task<OperationResult?> Handle(GetCurrentCritiqueOperationQuery request, CancellationToken cancellationToken)
    {
        var photograph = await photographs.FindOwnedAsync(request.PhotographId, owner.Id, cancellationToken)
            ?? throw new ResourceNotFoundException();
        if (photograph.CurrentCritiqueOperationId is not { } operationId) return null;
        var operation = await operations.FindOwnedAsync(operationId, owner.Id, cancellationToken)
            ?? throw new ResourceNotFoundException();
        return OperationResult.From(operation);
    }
}
