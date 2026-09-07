using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Photographs;

public sealed class UpdateBriefCommandHandler(ICurrentOwner owner, IPhotographStore photographs) : IRequestHandler<UpdateBriefCommand, PhotographResult>
{
    public async Task<PhotographResult> Handle(UpdateBriefCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the photograph revision you opened.");
        var brief = CritiqueBriefValidator.Normalize(request.Brief);
        return PhotographResult.From(await photographs.UpdateBriefAsync(request.Id, owner.Id, request.Revision, brief, cancellationToken));
    }
}
