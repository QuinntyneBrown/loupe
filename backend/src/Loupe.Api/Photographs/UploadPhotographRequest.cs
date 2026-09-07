namespace Loupe.Api.Photographs;

public sealed class UploadPhotographRequest
{
    public required IFormFile Image { get; init; }
    public string? Title { get; init; }
}
