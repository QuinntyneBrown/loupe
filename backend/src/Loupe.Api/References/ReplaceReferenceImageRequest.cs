namespace Loupe.Api.References;

public sealed class ReplaceReferenceImageRequest
{
    public required IFormFile Image { get; init; }
    public long Revision { get; init; }
}
