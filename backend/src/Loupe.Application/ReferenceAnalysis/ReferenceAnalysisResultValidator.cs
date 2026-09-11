using Loupe.Domain.References;
using System.Text;

namespace Loupe.Application.ReferenceAnalysis;

public static class ReferenceAnalysisResultValidator
{
    public static bool IsValid(ReferenceAnalysisResult? result)
    {
        if (result is null || string.IsNullOrWhiteSpace(result.Description) || result.Description.EnumerateRunes().Count() > 4000 || result.Tags is null || result.Tags.Length > 50) return false;
        var names = new HashSet<string>(StringComparer.Ordinal);
        return result.Tags.All(tag => tag is not null && !string.IsNullOrWhiteSpace(tag.Name) && tag.Name.EnumerateRunes().Count() <= 50
            && tag.Category is "subject" or "genre" or "composition" or "lighting" or "palette" or "mood" or "technique"
            && names.Add(tag.Name.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant()));
    }
}
