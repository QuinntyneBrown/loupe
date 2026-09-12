using Loupe.Application.Common;
using Loupe.Application.References;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Photographers;

public sealed class SetReferencePhotographerCommandHandler(ICurrentOwner owner, IReferencePhotographerStore links) : IRequestHandler<SetReferencePhotographerCommand, ReferenceResult>
{
    public async Task<ReferenceResult> Handle(SetReferencePhotographerCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the reference revision you opened.");
        return ReferenceResult.From(await links.SetAsync(owner.Id, request.ReferenceId, request.Revision, request.PhotographerId, cancellationToken));
    }
}
