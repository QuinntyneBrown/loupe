using Loupe.Application.Common;
using Loupe.Application.Operations;
using Loupe.Application.Photographers;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.PhotographerSummaries;

public sealed class GetPhotographerSummaryQueryHandler(ICurrentOwner owner, IPhotographerStore photographers,
    IBackgroundOperationStore operations) : IRequestHandler<GetPhotographerSummaryQuery, OperationResult?>
{
    public async Task<OperationResult?> Handle(GetPhotographerSummaryQuery request, CancellationToken cancellationToken)
    {
        var photographer = await photographers.FindOwnedAsync(owner.Id, request.PhotographerId, cancellationToken) ?? throw new ResourceNotFoundException();
        if (photographer.CurrentSummaryOperationId is not { } id) return null;
        return OperationResult.From(await operations.FindOwnedAsync(id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException());
    }
}
