using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Boards;

public sealed class ListBoardsQueryHandler(ICurrentOwner owner, IBoardStore boards) : IRequestHandler<ListBoardsQuery, IReadOnlyList<BoardResult>>
{
    public Task<IReadOnlyList<BoardResult>> Handle(ListBoardsQuery request, CancellationToken cancellationToken) => boards.ListAsync(owner.Id, cancellationToken);
}
