using Loupe.Domain.Locations;
using Loupe.Domain.Scouting;

namespace Loupe.Application.Scouting;

public static class ScoutingInputBuilder
{
    public static ScoutingInput From(Location location) => new(location.ImageSetRevision, location.ScoutingBrief,
        location.Images.OrderBy(image => image.Position).Select(image => new ScoutingImageInput(image.Id, image.Position, image.PreviewKey, image.Exif)).ToArray());
}
