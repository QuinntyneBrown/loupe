using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;
namespace Loupe.Application.Boards;
public sealed class RenameBoardCommandHandler(ICurrentOwner owner, IBoardStore boards) : IRequestHandler<RenameBoardCommand, BoardResult>
{
    public Task<BoardResult> Handle(RenameBoardCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the board revision you opened.");
        return boards.RenameAsync(owner.Id, request.Id, request.Revision, BoardName.Validate(request.Name), cancellationToken);
    }
}
