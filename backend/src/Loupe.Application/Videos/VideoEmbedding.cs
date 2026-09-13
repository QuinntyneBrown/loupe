using Loupe.Domain.Videos;

namespace Loupe.Application.Videos;

public static class VideoEmbedding
{
    public const int Dimensions = 1536;

    /// <summary>The text embedded for a video: never its private notes.</summary>
    public static string Text(Video video) => string.Join('\n', new[]
    {
        video.Title, "Topic: " + video.Topic, video.Channel is null ? null : "Channel: " + video.Channel, video.Summary,
        video.Tags.Count == 0 ? null : "Tags: " + string.Join(", ", video.Tags.OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase).Select(tag => tag.Name))
    }.Where(line => line is not null));
}
