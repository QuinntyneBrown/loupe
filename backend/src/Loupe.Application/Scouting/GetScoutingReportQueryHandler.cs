using Loupe.Application.Common;
using Loupe.Application.Locations;
using Loupe.Application.Security;
using Loupe.Domain.Scouting;
using MediatR;

namespace Loupe.Application.Scouting;

public sealed class GetScoutingReportQueryHandler(ICurrentOwner owner, ILocationStore locations) : IRequestHandler<GetScoutingReportQuery, SavedScoutingReport?>
{
    public async Task<SavedScoutingReport?> Handle(GetScoutingReportQuery request, CancellationToken cancellationToken)
    {
        var location = await locations.FindOwnedAsync(request.LocationId, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        return ScoutingReportJson.Deserialize(location.ScoutingReportJson);
    }
}
