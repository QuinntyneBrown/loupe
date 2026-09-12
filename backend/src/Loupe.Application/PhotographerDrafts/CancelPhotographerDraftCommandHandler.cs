using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.PhotographerDrafts;

public sealed class CancelPhotographerDraftCommandHandler(ICurrentOwner owner, IPhotographerDraftStore drafts) : IRequestHandler<CancelPhotographerDraftCommand>
{
    public Task Handle(CancelPhotographerDraftCommand request, CancellationToken cancellationToken) => drafts.CancelAsync(owner.Id, request.Id, cancellationToken);
}
