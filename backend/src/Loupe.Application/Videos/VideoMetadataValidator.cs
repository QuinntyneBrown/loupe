using Loupe.Application.Common;
using Loupe.Application.References;

namespace Loupe.Application.Videos;

public static class VideoMetadataValidator
{
    public static VideoMetadata Normalize(string? title, string? url, string? topic, string? channel, string? summary, string? notes, VideoTagInput[]? tags)
    {
        var normalizedTitle = TextField.Normalize(title, 200, "title") ?? throw new RequestValidationException("title", "Enter a video title.");
        var videoId = YouTubeVideo.ParseId(TextField.Normalize(url, 2048, "url"))
            ?? throw new RequestValidationException("url", "Use a YouTube video URL such as https://www.youtube.com/watch?v=… or https://youtu.be/….");
        var normalizedTopic = VideoTopic.Validate(topic);
        if (tags?.Length > 50 || tags?.Any(tag => tag is null) == true) throw new RequestValidationException("tags", "Supply up to 50 active tags.");
        var normalizedTags = (tags ?? []).Select(tag => new VideoTagInput(TagName.Validate(tag.Name), TagName.Category(tag.Category)))
            .DistinctBy(tag => tag.Name!.ToUpperInvariant()).ToArray();
        return new(normalizedTitle, videoId, normalizedTopic, TextField.Normalize(channel, 200, "channel"),
            TextField.Normalize(summary, 4000, "summary"), TextField.Normalize(notes, 10000, "notes"), normalizedTags);
    }
}
