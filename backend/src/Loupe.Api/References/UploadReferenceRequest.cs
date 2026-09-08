namespace Loupe.Api.References;

public sealed class UploadReferenceRequest
{
    public required IFormFile Image { get; init; }
    public string? Title { get; init; }
    public string? SourceUrl { get; init; }
    public string? Attribution { get; init; }
    public string? Notes { get; init; }
}
