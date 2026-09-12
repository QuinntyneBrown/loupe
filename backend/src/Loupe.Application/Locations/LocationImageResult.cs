using Loupe.Domain.Locations;

namespace Loupe.Application.Locations;

public sealed record LocationImageResult(Guid Id, int Position, string ImageUrl, string PreviewUrl, int Width, int Height)
{
    public static LocationImageResult From(Guid locationId, LocationImage image) =>
        new(image.Id, image.Position, LocationImageUrls.Image(locationId, image.Id), LocationImageUrls.Preview(locationId, image.Id), image.Width, image.Height);
}
