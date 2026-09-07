using Loupe.Domain.Photographs;

namespace Loupe.Application.Photographs;

public sealed record PhotographResult(Guid Id, string Title, DateTimeOffset CreatedAt, int Width, int Height, string ImageUrl, string PreviewUrl, CaptureMetadata Exif, CritiqueBrief Brief)
{
    public static PhotographResult From(Photograph photograph) => new(photograph.Id, photograph.Title, photograph.CreatedAt,
        photograph.Width, photograph.Height, $"/api/photographs/{photograph.Id}/image", $"/api/photographs/{photograph.Id}/preview", photograph.Exif, photograph.Brief);
}
