using Loupe.Application.Common;
using Loupe.Application.Deletions;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Photographs;

public sealed class DeletePhotographCommandHandler(ICurrentOwner owner, IDeletionStore deletions) : IRequestHandler<DeletePhotographCommand, DeletionResult>
{
    public async Task<DeletionResult> Handle(DeletePhotographCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the photograph revision you opened.");
        return DeletionResult.From(await deletions.DeletePhotographAsync(request.Id, owner.Id, request.Revision, cancellationToken));
    }
}
