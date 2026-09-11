using Loupe.Application.Common;
using Loupe.Application.Images;
using Loupe.Application.Security;
using MediatR;
namespace Loupe.Application.ReferenceDrafts;

public sealed class GetReferenceDraftImageQueryHandler(ICurrentOwner owner, IReferenceDraftStore drafts, IImageStore images) : IRequestHandler<GetReferenceDraftImageQuery, ImageContent>
{
    public async Task<ImageContent> Handle(GetReferenceDraftImageQuery request, CancellationToken cancellationToken)
    {
        var draft = await drafts.FindOwnedAsync(owner.Id, request.Id, cancellationToken);
        var key = (request.Preview ? draft.PreviewKey : draft.ImageKey) ?? throw new ResourceNotFoundException();
        return new ImageContent(images.OpenRead(key), request.Preview ? "image/jpeg" : "image/png");
    }
}
