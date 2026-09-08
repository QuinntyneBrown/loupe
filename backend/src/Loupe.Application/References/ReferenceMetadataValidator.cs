using Loupe.Application.Common;

namespace Loupe.Application.References;

public static class ReferenceMetadataValidator
{
    public static ReferenceMetadata Normalize(string? title, string? sourceUrl, string? attribution, string? notes)
    {
        var normalizedTitle = TextField.Normalize(title, 200, "title")
            ?? throw new RequestValidationException("title", "Enter a title.");
        var source = TextField.Normalize(sourceUrl, 2048, "sourceUrl");
        if (source is not null && (source.Contains('\\') || source.Any(char.IsWhiteSpace)
            || !Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo)))
            throw new RequestValidationException("sourceUrl", "Use an absolute HTTP or HTTPS URL on its standard port, without credentials.");
        return new ReferenceMetadata(normalizedTitle, source, TextField.Normalize(attribution, 200, "attribution"),
            TextField.Normalize(notes, 10000, "notes"));
    }
}
