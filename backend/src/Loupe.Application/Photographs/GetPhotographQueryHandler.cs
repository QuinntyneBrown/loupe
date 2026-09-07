using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Photographs;

public sealed class GetPhotographQueryHandler(ICurrentOwner owner, IPhotographStore photographs) : IRequestHandler<GetPhotographQuery, PhotographResult>
{
    public async Task<PhotographResult> Handle(GetPhotographQuery request, CancellationToken cancellationToken) =>
        PhotographResult.From(await photographs.FindOwnedAsync(request.Id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException());
}
