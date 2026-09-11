using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.PhotographerDrafts;

public sealed class GetPhotographerDraftQueryHandler(ICurrentOwner owner, IPhotographerDraftStore drafts) : IRequestHandler<GetPhotographerDraftQuery, PhotographerDraftResult>
{
    public async Task<PhotographerDraftResult> Handle(GetPhotographerDraftQuery request, CancellationToken cancellationToken) =>
        PhotographerDraftResult.From(await drafts.FindOwnedAsync(owner.Id, request.Id, cancellationToken));
}
