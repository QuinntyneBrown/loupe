using Loupe.Application.Common;
using Loupe.Application.Deletions;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.References;

public sealed class DeleteReferenceCommandHandler(ICurrentOwner owner, IDeletionStore deletions) : IRequestHandler<DeleteReferenceCommand, DeletionResult>
{
    public async Task<DeletionResult> Handle(DeleteReferenceCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the reference revision you opened.");
        return DeletionResult.From(await deletions.DeleteReferenceAsync(request.Id, owner.Id, request.Revision, cancellationToken));
    }
}
