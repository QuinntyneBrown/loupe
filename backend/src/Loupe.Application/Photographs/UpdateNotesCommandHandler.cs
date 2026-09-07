using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Photographs;

public sealed class UpdateNotesCommandHandler(ICurrentOwner owner, IPhotographStore photographs) : IRequestHandler<UpdateNotesCommand, PhotographResult>
{
    public async Task<PhotographResult> Handle(UpdateNotesCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the photograph revision you opened.");
        var notes = TextField.Normalize(request.Notes, 10000, "notes");
        return PhotographResult.From(await photographs.UpdateNotesAsync(request.Id, owner.Id, request.Revision, notes, cancellationToken));
    }
}
