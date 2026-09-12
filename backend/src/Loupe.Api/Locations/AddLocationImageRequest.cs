namespace Loupe.Api.Locations;

public sealed class AddLocationImageRequest
{
    public required IFormFile Image { get; init; }
}
