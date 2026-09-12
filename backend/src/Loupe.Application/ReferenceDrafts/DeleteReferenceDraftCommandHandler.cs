using Loupe.Application.Security;
using MediatR;
namespace Loupe.Application.ReferenceDrafts;

public sealed class DeleteReferenceDraftCommandHandler(ICurrentOwner owner, IReferenceDraftStore drafts) : IRequestHandler<DeleteReferenceDraftCommand>
{
    public Task Handle(DeleteReferenceDraftCommand request, CancellationToken cancellationToken) => drafts.DeleteAsync(owner.Id, request.Id, cancellationToken);
}
