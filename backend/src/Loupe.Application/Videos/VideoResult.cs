using Loupe.Domain.Videos;

namespace Loupe.Application.Videos;

public sealed record VideoResult(Guid Id, string Title, string Url, string VideoId, string ThumbnailUrl, string Topic, string? Channel,
    string? Summary, string? Notes, DateTimeOffset CreatedAt, long Revision, VideoTagResult[] Tags, bool Indexed, double? Score)
{
    public static VideoResult From(Video video, string? currentModel, double? score = null) => new(video.Id, video.Title, YouTubeVideo.CanonicalUrl(video.VideoId), video.VideoId,
        YouTubeVideo.ThumbnailUrl(video.VideoId), video.Topic, video.Channel, video.Summary, video.Notes, video.CreatedAt, video.Revision,
        video.Tags.OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase).Select(tag => new VideoTagResult(tag.Name, tag.Category, tag.Provenance)).ToArray(),
        video.EmbeddedRevision == video.Revision && video.EmbeddingModel is not null && video.EmbeddingModel == currentModel, score);
}
