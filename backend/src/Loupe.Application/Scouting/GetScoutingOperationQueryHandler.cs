using Loupe.Application.Common;
using Loupe.Application.Locations;
using Loupe.Application.Operations;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Scouting;

public sealed class GetScoutingOperationQueryHandler(ICurrentOwner owner, ILocationStore locations, IBackgroundOperationStore operations)
    : IRequestHandler<GetScoutingOperationQuery, OperationResult?>
{
    public async Task<OperationResult?> Handle(GetScoutingOperationQuery request, CancellationToken cancellationToken)
    {
        var location = await locations.FindOwnedAsync(request.LocationId, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        if (location.CurrentScoutingOperationId is not { } id) return null;
        return OperationResult.From(await operations.FindOwnedAsync(id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException());
    }
}
