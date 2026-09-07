using System.Text;

namespace Loupe.Application.Common;

public static class TextField
{
    public static string? Normalize(string? value, int maximum, string field)
    {
        var normalized = Clean(value);
        if (normalized.EnumerateRunes().Take(maximum + 1).Count() > maximum)
            throw new RequestValidationException(field, $"Use {maximum} characters or fewer.");
        return normalized.Length == 0 ? null : normalized;
    }

    public static string Default(string? value, int maximum, string fallback)
    {
        var normalized = string.Concat(Clean(value).EnumerateRunes().Take(maximum));
        return normalized.Length == 0 ? fallback : normalized;
    }

    private static string Clean(string? value) => (value ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
}
