using System.Text.Json;
using AngleSharp.Dom;

namespace Loupe.Infrastructure.ReferenceImports;

public static class SourcePageRestrictions
{
    public static bool DeniesAccess(IDocument document)
    {
        if (document.QuerySelector("input[type=password]") is not null) return true;
        if (document.QuerySelectorAll("[itemprop~='isAccessibleForFree'], [property$='isAccessibleForFree']")
            .Any(element => IsFalse(element.GetAttribute("content") ?? element.TextContent))) return true;
        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
        {
            try
            {
                using var json = JsonDocument.Parse(script.TextContent);
                if (Restricted(json.RootElement)) return true;
            }
            catch (JsonException)
            {
                // An unreadable declaration of access is not permission to import.
                if (script.TextContent.Contains("isAccessibleForFree", StringComparison.Ordinal)) return true;
            }
        }
        return false;
    }

    private static bool Restricted(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().Any(property =>
            property.Name == "isAccessibleForFree" && (property.Value.ValueKind == JsonValueKind.False
                || property.Value.ValueKind == JsonValueKind.String && IsFalse(property.Value.GetString()))
            || Restricted(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().Any(Restricted),
        _ => false
    };

    private static bool IsFalse(string? value) => string.Equals(value?.Trim(), "false", StringComparison.OrdinalIgnoreCase);
}
