using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.References;

public sealed class GetReferenceQueryHandler(ICurrentOwner owner, IReferenceStore references) : IRequestHandler<GetReferenceQuery, ReferenceResult>
{
    public async Task<ReferenceResult> Handle(GetReferenceQuery request, CancellationToken cancellationToken) =>
        ReferenceResult.From(await references.FindOwnedAsync(request.Id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException());
}
