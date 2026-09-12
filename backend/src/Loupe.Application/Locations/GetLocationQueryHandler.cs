using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Locations;

public sealed class GetLocationQueryHandler(ICurrentOwner owner, ILocationStore locations) : IRequestHandler<GetLocationQuery, LocationResult>
{
    public async Task<LocationResult> Handle(GetLocationQuery request, CancellationToken cancellationToken) =>
        LocationResult.From(await locations.FindOwnedAsync(request.Id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException());
}
