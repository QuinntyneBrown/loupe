using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;
namespace Loupe.Application.Boards;
public sealed class DeleteBoardCommandHandler(ICurrentOwner owner, IBoardStore boards) : IRequestHandler<DeleteBoardCommand>
{
    public Task Handle(DeleteBoardCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the board revision you opened.");
        return boards.DeleteAsync(owner.Id, request.Id, request.Revision, cancellationToken);
    }
}
