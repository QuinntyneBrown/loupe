using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Deletions;

public sealed class GetDeletionQueryHandler(ICurrentOwner owner, IDeletionStore deletions) : IRequestHandler<GetDeletionQuery, DeletionResult>
{
    public async Task<DeletionResult> Handle(GetDeletionQuery request, CancellationToken cancellationToken) =>
        DeletionResult.From(await deletions.FindOwnedAsync(request.Id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException());
}
