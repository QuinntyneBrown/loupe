using Loupe.Application.Common;
using Loupe.Application.References;

namespace Loupe.Application.Photographers;

public static class PhotographerMetadataValidator
{
    public static PhotographerMetadata Normalize(string? name, string? portfolioUrl, string? summary, string? notes, PhotographerTagInput[]? tags, IPortfolioUrlPolicy policy)
    {
        var normalizedName = TextField.Normalize(name, 200, "name") ?? throw new RequestValidationException("name", "Enter a photographer name.");
        var source = TextField.Normalize(portfolioUrl, 2048, "portfolioUrl");
        if (source is null || source.Contains('\\') || source.Any(char.IsWhiteSpace) || !Uri.TryCreate(source, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo) || !policy.IsAllowed(uri))
            throw new RequestValidationException("portfolioUrl", "Use a public HTTP or HTTPS portfolio URL on its standard port, without credentials.");
        if (tags?.Length > 50 || tags?.Any(tag => tag is null) == true) throw new RequestValidationException("tags", "Supply up to 50 active tags.");
        var normalizedTags = (tags ?? []).Select(tag => new PhotographerTagInput(TagName.Validate(tag.Name), TagName.Category(tag.Category))).DistinctBy(tag => tag.Name!.ToUpperInvariant()).ToArray();
        return new(normalizedName, source, TextField.Normalize(summary, 4000, "summary"), TextField.Normalize(notes, 10000, "notes"), normalizedTags);
    }
}
