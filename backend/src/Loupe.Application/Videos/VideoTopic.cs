using Loupe.Application.Common;

namespace Loupe.Application.Videos;

public static class VideoTopic
{
    public static readonly string[] All = ["posing", "lighting", "interview", "composition", "editing", "gear", "other"];

    public static string Validate(string? value) => value is not null && All.Contains(value, StringComparer.Ordinal) ? value
        : throw new RequestValidationException("topic", "Choose a topic: posing, lighting, interview, composition, editing, gear, or other.");
}
