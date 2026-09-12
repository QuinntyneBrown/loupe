using Loupe.Application.Common;
using Loupe.Application.Deletions;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Photographers;

public sealed class DeletePhotographerCommandHandler(ICurrentOwner owner, IDeletionStore deletions) : IRequestHandler<DeletePhotographerCommand, DeletionResult>
{
    public async Task<DeletionResult> Handle(DeletePhotographerCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the bookmark revision you opened.");
        return DeletionResult.From(await deletions.DeletePhotographerAsync(request.Id, owner.Id, request.Revision, cancellationToken));
    }
}
