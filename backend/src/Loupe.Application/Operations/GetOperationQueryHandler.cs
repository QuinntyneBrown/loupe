using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Operations;

public sealed class GetOperationQueryHandler(ICurrentOwner owner, IBackgroundOperationStore operations) : IRequestHandler<GetOperationQuery, OperationResult>
{
    public async Task<OperationResult> Handle(GetOperationQuery request, CancellationToken cancellationToken) =>
        OperationResult.From(await operations.FindOwnedAsync(request.Id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException());
}
