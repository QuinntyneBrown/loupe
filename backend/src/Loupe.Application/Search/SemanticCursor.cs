using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Loupe.Application.Common;

namespace Loupe.Application.Search;

/// <summary>Keyset position in a similarity ranking (score descending, identifier ascending), bound to a search scope and the embedding model that produced the scores.</summary>
public sealed record SemanticCursor(double Score, Guid Id)
{
    public string Encode(string scope, string model) => scope + Generation(model) + "." +
        Convert.ToBase64String(Encoding.UTF8.GetBytes(FormattableString.Invariant($"{Score:R}:{Id:N}")));

    public static SemanticCursor? Parse(string? value, string scope, string model)
    {
        if (value is null) return null;
        if (value.Length > 200 || !value.StartsWith(scope, StringComparison.Ordinal))
            throw new RequestValidationException("cursor", "This cursor belongs to a different search. Refresh the results.");
        var rest = value[scope.Length..];
        var separator = rest.IndexOf('.');
        if (separator < 0) throw new RequestValidationException("cursor", "This continuation cursor is invalid. Refresh the list.");
        if (rest[..separator] != Generation(model)) throw new SearchRefreshRequiredException();
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(rest[(separator + 1)..])).Split(':');
            if (parts.Length != 2 || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var score)
                || !double.IsFinite(score) || !Guid.TryParseExact(parts[1], "N", out var id)) throw new FormatException();
            return new SemanticCursor(score, id);
        }
        catch (FormatException) { throw new RequestValidationException("cursor", "This continuation cursor is invalid. Refresh the list."); }
    }

    private static string Generation(string model) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(model)))[..16];
}
