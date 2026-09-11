using Loupe.Application.Common;
using Loupe.Application.References;
using Loupe.Application.Security;
using MediatR;
namespace Loupe.Application.Boards;
public sealed class SetReferenceBoardsCommandHandler(ICurrentOwner owner, IBoardStore boards) : IRequestHandler<SetReferenceBoardsCommand, ReferenceResult>
{
    public async Task<ReferenceResult> Handle(SetReferenceBoardsCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the reference revision you opened.");
        if (request.BoardIds is null) throw new RequestValidationException("boardIds", "Supply the selected boards, or an empty list.");
        return ReferenceResult.From(await boards.SetMembershipsAsync(owner.Id, request.ReferenceId, request.Revision,
            request.BoardIds.Distinct().ToArray(), cancellationToken));
    }
}
