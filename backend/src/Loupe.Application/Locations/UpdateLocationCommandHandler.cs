using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Locations;

public sealed class UpdateLocationCommandHandler(ICurrentOwner owner, ILocationStore locations) : IRequestHandler<UpdateLocationCommand, LocationResult>
{
    public async Task<LocationResult> Handle(UpdateLocationCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the location revision you opened.");
        var details = LocationDetailsValidator.Normalize(request.Details);
        return LocationResult.From(await locations.UpdateAsync(request.Id, owner.Id, request.Revision, details, cancellationToken));
    }
}
