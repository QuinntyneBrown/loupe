using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Locations;

public sealed class SetLocationTagsCommandHandler(ICurrentOwner owner, ILocationStore locations) : IRequestHandler<SetLocationTagsCommand, LocationResult>
{
    public async Task<LocationResult> Handle(SetLocationTagsCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the location revision you opened.");
        var tags = LocationDetailsValidator.Tags(request.Tags);
        return LocationResult.From(await locations.ReplaceTagsAsync(request.Id, owner.Id, request.Revision, tags, cancellationToken));
    }
}
