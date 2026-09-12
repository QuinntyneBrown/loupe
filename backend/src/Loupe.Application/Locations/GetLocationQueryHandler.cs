using Loupe.Application.Common;
using Loupe.Application.Security;
using Loupe.Application.Search;
using MediatR;

namespace Loupe.Application.Locations;

public sealed class GetLocationQueryHandler(ICurrentOwner owner, ILocationStore locations, IEmbeddingConfiguration embeddings) : IRequestHandler<GetLocationQuery, LocationResult>
{
    public async Task<LocationResult> Handle(GetLocationQuery request, CancellationToken cancellationToken) =>
        LocationResult.From(await locations.FindOwnedAsync(request.Id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException(), embeddings.IsConfigured);
}
