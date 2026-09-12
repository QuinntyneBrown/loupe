using Loupe.Application.Boards;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.References;

public sealed class ListReferenceTagsQueryHandler(ICurrentOwner owner, IBoardStore boards, IReferenceTagStore tags)
    : IRequestHandler<ListReferenceTagsQuery, IReadOnlyList<ReferenceTagFacet>>
{
    public async Task<IReadOnlyList<ReferenceTagFacet>> Handle(ListReferenceTagsQuery request, CancellationToken cancellationToken)
    {
        if (request.BoardId is { } boardId) await boards.RequireOwnedAsync(owner.Id, boardId, cancellationToken);
        return await tags.ListAsync(owner.Id, request.BoardId, cancellationToken);
    }
}
