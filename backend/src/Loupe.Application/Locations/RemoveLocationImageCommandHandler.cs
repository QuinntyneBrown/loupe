using Loupe.Application.Common;
using Loupe.Application.Security;
using Loupe.Application.Search;
using MediatR;

namespace Loupe.Application.Locations;

public sealed class RemoveLocationImageCommandHandler(ICurrentOwner owner, ILocationImageStore images, IEmbeddingConfiguration embeddings) : IRequestHandler<RemoveLocationImageCommand, LocationResult>
{
    public async Task<LocationResult> Handle(RemoveLocationImageCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the location revision you opened.");
        return LocationResult.From(await images.RemoveAsync(request.Id, owner.Id, request.ImageId, request.Revision, cancellationToken), embeddings.IsConfigured);
    }
}
