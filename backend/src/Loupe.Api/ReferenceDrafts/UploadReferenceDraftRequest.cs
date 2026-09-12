namespace Loupe.Api.ReferenceDrafts;

public sealed class UploadReferenceDraftRequest
{
    public required IFormFile Image { get; init; }
    public string? SourceUrl { get; init; }
}
