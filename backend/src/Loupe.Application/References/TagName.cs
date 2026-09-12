using System.Text;
using Loupe.Application.Common;

namespace Loupe.Application.References;

public static class TagName
{
    public static string Validate(string? value) => TextField.Normalize(value?.Normalize(NormalizationForm.FormC), 50, "tags")
        ?? throw new RequestValidationException("tags", "Enter a tag name.");

    public static string? Category(string? value) => value is null or "subject" or "genre" or "composition" or "lighting" or "palette" or "mood" or "technique"
        ? value : throw new RequestValidationException("tags", "Choose a supported tag category.");
}
