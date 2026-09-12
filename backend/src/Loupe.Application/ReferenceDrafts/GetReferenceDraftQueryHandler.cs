using Loupe.Application.Security;
using MediatR;
namespace Loupe.Application.ReferenceDrafts;

public sealed class GetReferenceDraftQueryHandler(ICurrentOwner owner, IReferenceDraftStore drafts) : IRequestHandler<GetReferenceDraftQuery, ReferenceDraftResult>
{
    public async Task<ReferenceDraftResult> Handle(GetReferenceDraftQuery request, CancellationToken cancellationToken) =>
        ReferenceDraftResult.From(await drafts.FindOwnedAsync(owner.Id, request.Id, cancellationToken));
}
