using Loupe.Application.Common;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Photographers;

public sealed class UpdatePhotographerCommandHandler(ICurrentOwner owner, IPhotographerStore photographers, IPortfolioUrlPolicy policy) : IRequestHandler<UpdatePhotographerCommand, PhotographerResult>
{
    public async Task<PhotographerResult> Handle(UpdatePhotographerCommand request, CancellationToken cancellationToken)
    {
        if (request.Revision < 1) throw new RequestValidationException("revision", "Supply the bookmark revision you opened.");
        var metadata = PhotographerMetadataValidator.Normalize(request.Name, request.PortfolioUrl, request.Summary, request.Notes, request.Tags, policy);
        return PhotographerResult.From(await photographers.UpdateAsync(owner.Id, request.Id, request.Revision, metadata, cancellationToken));
    }
}
