using Loupe.Application.Common;
using Loupe.Application.Locations;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Scouting;

public sealed class GetScoutingReportQueryHandler(ICurrentOwner owner, ILocationStore locations) : IRequestHandler<GetScoutingReportQuery, object?>
{
    public async Task<object?> Handle(GetScoutingReportQuery request, CancellationToken cancellationToken)
    {
        var location = await locations.FindOwnedAsync(request.LocationId, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        // The saved report shape arrives with report generation; until then a location never holds one.
        return location.ScoutingReportJson is null ? null : new { };
    }
}
