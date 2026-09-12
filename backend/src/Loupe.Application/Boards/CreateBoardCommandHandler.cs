using Loupe.Application.Security;
using Loupe.Domain.Boards;
using MediatR;

namespace Loupe.Application.Boards;

public sealed class CreateBoardCommandHandler(ICurrentOwner owner, IBoardStore boards) : IRequestHandler<CreateBoardCommand, BoardResult>
{
    public async Task<BoardResult> Handle(CreateBoardCommand request, CancellationToken cancellationToken)
    {
        var name = BoardName.Validate(request.Name);
        var board = new Board { OwnerId = owner.Id, Name = name, NormalizedName = name.ToUpperInvariant() };
        await boards.CreateAsync(board, cancellationToken);
        return new BoardResult(board.Id, board.Name, board.Revision, board.References.Count);
    }
}
