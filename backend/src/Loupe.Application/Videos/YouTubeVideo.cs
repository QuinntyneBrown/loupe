using System.Text.RegularExpressions;
using System.Web;

namespace Loupe.Application.Videos;

public static partial class YouTubeVideo
{
    private static readonly string[] Hosts = ["youtube.com", "www.youtube.com", "m.youtube.com", "music.youtube.com", "youtube-nocookie.com", "www.youtube-nocookie.com"];
    private static readonly string[] ShortHosts = ["youtu.be", "www.youtu.be"];
    private static readonly string[] PathPrefixes = ["/shorts/", "/live/", "/embed/", "/v/"];

    public static string? ParseId(string? url)
    {
        if (url is null || url.Any(char.IsWhiteSpace) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo)) return null;
        var host = uri.Host.ToLowerInvariant();
        string? candidate = null;
        if (ShortHosts.Contains(host)) candidate = uri.AbsolutePath.Trim('/');
        else if (Hosts.Contains(host))
        {
            if (uri.AbsolutePath == "/watch") candidate = HttpUtility.ParseQueryString(uri.Query).Get("v");
            else candidate = PathPrefixes.Where(prefix => uri.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal))
                .Select(prefix => uri.AbsolutePath[prefix.Length..].Trim('/')).FirstOrDefault();
        }
        return candidate is not null && IdPattern().IsMatch(candidate) ? candidate : null;
    }

    public static string CanonicalUrl(string videoId) => "https://www.youtube.com/watch?v=" + videoId;

    public static string ThumbnailUrl(string videoId) => "https://i.ytimg.com/vi/" + videoId + "/hqdefault.jpg";

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex IdPattern();
}
