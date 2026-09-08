using Loupe.Application.Common;
using Loupe.Application.Images;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.References;

public sealed class GetReferenceImageQueryHandler(ICurrentOwner owner, IReferenceStore references, IImageStore images)
    : IRequestHandler<GetReferenceImageQuery, ImageContent>
{
    public async Task<ImageContent> Handle(GetReferenceImageQuery request, CancellationToken cancellationToken)
    {
        var reference = await references.FindOwnedAsync(request.Id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        var key = (request.Preview ? reference.PreviewKey : reference.ImageKey) ?? throw new ResourceNotFoundException();
        return new ImageContent(images.OpenRead(key), request.Preview ? "image/jpeg" : "image/png");
    }
}
