using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Locations;

public sealed class UpdateLocationTextCommandHandler(ICurrentOwner owner, ILocationStore locations) : IRequestHandler<UpdateLocationTextCommand, LocationResult>
{
    public async Task<LocationResult> Handle(UpdateLocationTextCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the location revision you opened.");
        var text = LocationDetailsValidator.Text(request.Field, request.Text);
        return LocationResult.From(await locations.UpdateTextAsync(request.Id, owner.Id, request.Revision, request.Field, text, cancellationToken));
    }
}
