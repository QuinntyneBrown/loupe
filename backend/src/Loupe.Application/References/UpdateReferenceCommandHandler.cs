using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.References;

public sealed class UpdateReferenceCommandHandler(ICurrentOwner owner, IReferenceStore references) : IRequestHandler<UpdateReferenceCommand, ReferenceResult>
{
    public async Task<ReferenceResult> Handle(UpdateReferenceCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the reference revision you opened.");
        var metadata = ReferenceMetadataValidator.Normalize(request.Title, request.SourceUrl, request.Attribution, request.Notes);
        return ReferenceResult.From(await references.UpdateAsync(request.Id, owner.Id, request.Revision, metadata, cancellationToken));
    }
}
