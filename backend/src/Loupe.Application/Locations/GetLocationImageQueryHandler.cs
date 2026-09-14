using Loupe.Application.Common;
using Loupe.Application.Images;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Locations;

public sealed class GetLocationImageQueryHandler(ICurrentOwner owner, ILocationStore locations, IImageStore images) : IRequestHandler<GetLocationImageQuery, ImageContent>
{
    public async Task<ImageContent> Handle(GetLocationImageQuery request, CancellationToken cancellationToken)
    {
        var location = await locations.FindOwnedAsync(request.Id, owner.Id, cancellationToken) ?? throw new ResourceNotFoundException();
        var image = location.Images.SingleOrDefault(image => image.Id == request.ImageId) ?? throw new ResourceNotFoundException();
        return new ImageContent(images.OpenRead(request.Preview ? image.PreviewKey : image.ImageKey), request.Preview ? "image/jpeg" : "image/png");
    }
}
