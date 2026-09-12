using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Locations;

public sealed class ListLocationsQueryHandler(ICurrentOwner owner, ILocationStore locations) : IRequestHandler<ListLocationsQuery, LocationPage>
{
    public async Task<LocationPage> Handle(ListLocationsQuery request, CancellationToken cancellationToken)
    {
        if (request.PageSize is < 1 or > 100) throw new RequestValidationException("pageSize", "Choose between 1 and 100 items.");
        var scope = LocationListCursor.Scope(owner.Id, request.PageSize);
        var found = await locations.ListAsync(owner.Id, request.PageSize + 1, LocationListCursor.Parse(request.Cursor, scope), cancellationToken);
        var items = found.Take(request.PageSize).ToArray();
        var next = found.Count > request.PageSize ? LocationListCursor.Encode(new CreatedCursor(items[^1].CreatedAt, items[^1].Id), scope) : null;
        return new LocationPage(items, next, await locations.CountAsync(owner.Id, cancellationToken));
    }
}
