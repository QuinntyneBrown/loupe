using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Loupe.Application.ReferenceImports;

namespace Loupe.Infrastructure.ReferenceImports;

public sealed class RobotsPolicy(IRestrictedPageFetcher fetcher) : IRobotsPolicy
{
    public async Task<RobotsDecision> EvaluateAsync(Uri target, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await fetcher.FetchAsync(new Uri(target.GetLeftPart(UriPartial.Authority) + "/robots.txt"), cancellationToken);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) return RobotsDecision.Allowed;
            if (!response.IsSuccessStatusCode) return RobotsDecision.Unavailable;
            var bytes = await BoundedSourceContent.ReadAsync(response.Content, 512_000, cancellationToken);
            return Evaluate(Encoding.UTF8.GetString(bytes), target.PathAndQuery);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return RobotsDecision.Unavailable; }
        catch (Exception failure) when (failure is HttpRequestException or IOException or InvalidDataException or SourceFetchException or RegexMatchTimeoutException)
        { return RobotsDecision.Unavailable; }
    }

    private static RobotsDecision Evaluate(string text, string path)
    {
        var groups = new List<(List<string> Agents, List<(string Path, bool Allow)> Rules)>();
        var agents = new List<string>();
        var rules = new List<(string Path, bool Allow)>();
        var hasRules = false;
        foreach (var raw in text.TrimStart('\uFEFF').Split('\n'))
        {
            var line = raw.Split('#', 2)[0].Trim();
            var colon = line.IndexOf(':');
            if (colon < 0) continue;
            var field = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (field.Equals("User-agent", StringComparison.OrdinalIgnoreCase))
            {
                if (hasRules)
                {
                    groups.Add((agents, rules));
                    agents = [];
                    rules = [];
                    hasRules = false;
                }
                agents.Add(value);
            }
            else if (agents.Count > 0 && (field.Equals("Allow", StringComparison.OrdinalIgnoreCase) || field.Equals("Disallow", StringComparison.OrdinalIgnoreCase)))
            {
                hasRules = true;
                if (value.StartsWith('/')) rules.Add((Normalize(value), field.Equals("Allow", StringComparison.OrdinalIgnoreCase)));
            }
        }
        groups.Add((agents, rules));
        var specific = groups.Where(group => group.Agents.Any(agent => agent.Equals("Loupe", StringComparison.OrdinalIgnoreCase))).ToArray();
        var applicable = specific.Length > 0 ? specific : groups.Where(group => group.Agents.Contains("*")).ToArray();
        var normalizedPath = Normalize(path);
        var bestLength = -1;
        var allowed = true;
        foreach (var rule in applicable.SelectMany(group => group.Rules))
        {
            var anchored = rule.Path.EndsWith('$');
            var pattern = anchored ? rule.Path[..^1] : rule.Path;
            var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + (anchored ? "$" : "");
            if (!Regex.IsMatch(normalizedPath, regex, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) continue;
            var length = Encoding.UTF8.GetByteCount(pattern.Replace("*", ""));
            if (length > bestLength) { bestLength = length; allowed = rule.Allow; }
            else if (length == bestLength && rule.Allow) allowed = true;
        }
        return allowed ? RobotsDecision.Allowed : RobotsDecision.Disallowed;
    }

    private static string Normalize(string value)
    {
        var result = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            if (character == '%' && i + 2 < value.Length && byte.TryParse(value.AsSpan(i + 1, 2), System.Globalization.NumberStyles.HexNumber, null, out var octet))
            {
                var decoded = (char)octet;
                if (char.IsAsciiLetterOrDigit(decoded) || "-._~".Contains(decoded)) result.Append(decoded);
                else result.Append('%').Append(octet.ToString("X2"));
                i += 2;
            }
            else if (character > 127)
            {
                var count = char.IsHighSurrogate(character) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]) ? 2 : 1;
                foreach (var encoded in Encoding.UTF8.GetBytes(value.Substring(i, count))) result.Append('%').Append(encoded.ToString("X2"));
                i += count - 1;
            }
            else result.Append(character);
        }
        return result.ToString();
    }
}
