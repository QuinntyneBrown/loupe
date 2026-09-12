using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Photographers;

public sealed class GetPhotographerQueryHandler(ICurrentOwner owner, IPhotographerStore photographers) : IRequestHandler<GetPhotographerQuery, PhotographerResult>
{
    public async Task<PhotographerResult> Handle(GetPhotographerQuery request, CancellationToken cancellationToken) =>
        PhotographerResult.From(await photographers.FindOwnedAsync(owner.Id, request.Id, cancellationToken) ?? throw new ResourceNotFoundException());
}
