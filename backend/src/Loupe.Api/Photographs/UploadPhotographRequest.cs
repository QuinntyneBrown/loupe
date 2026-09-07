namespace Loupe.Api.Photographs;

public sealed class UploadPhotographRequest
{
    public required IFormFile Image { get; init; }
    public string? Title { get; init; }
    public string? Intent { get; init; }
    public string? Genre { get; init; }
    public string? Experience { get; init; }
    public string? RequestedFeedback { get; init; }
}
