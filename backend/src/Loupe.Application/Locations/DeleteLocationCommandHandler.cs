using Loupe.Application.Common;
using Loupe.Application.Deletions;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Locations;

public sealed class DeleteLocationCommandHandler(ICurrentOwner owner, IDeletionStore deletions) : IRequestHandler<DeleteLocationCommand, DeletionResult>
{
    public async Task<DeletionResult> Handle(DeleteLocationCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the location revision you opened.");
        return DeletionResult.From(await deletions.DeleteLocationAsync(request.Id, owner.Id, request.Revision, cancellationToken));
    }
}
