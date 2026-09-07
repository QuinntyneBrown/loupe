using Loupe.Application.Common;
using Loupe.Application.Images;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Photographs;

public sealed class GetPhotographImageQueryHandler(ICurrentOwner owner, IPhotographStore photographs, IImageStore images)
    : IRequestHandler<GetPhotographImageQuery, ImageContent>
{
    public async Task<ImageContent> Handle(GetPhotographImageQuery request, CancellationToken cancellationToken)
    {
        var photograph = await photographs.FindOwnedAsync(request.Id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        return new ImageContent(images.OpenRead(request.Preview ? photograph.PreviewKey : photograph.ImageKey), request.Preview ? "image/jpeg" : "image/png");
    }
}
