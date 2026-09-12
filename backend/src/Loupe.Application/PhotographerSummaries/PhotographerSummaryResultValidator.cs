using System.Text;
using Loupe.Domain.Photographers;

namespace Loupe.Application.PhotographerSummaries;

public static class PhotographerSummaryResultValidator
{
    public static bool IsValid(PhotographerSummaryResult? result, CapturedPortfolioPage source)
    {
        if (result?.Tags is null || result.Tags.Length > 50) return false;
        if (result.UnavailableReason is not null) return result.UnavailableReason == "insufficient_information" && result.Summary is null && result.Tags.Length == 0;
        if (string.IsNullOrWhiteSpace(source.MainText) && string.IsNullOrWhiteSpace(source.Description)) return false;
        if (string.IsNullOrWhiteSpace(result.Summary) || result.Summary.Contains('\0') || result.Summary.EnumerateRunes().Count() > 4000) return false;
        var names = new HashSet<string>(StringComparer.Ordinal);
        return result.Tags.All(tag => tag is not null && !string.IsNullOrWhiteSpace(tag.Name) && !tag.Name.Contains('\0') && tag.Name.EnumerateRunes().Count() <= 50
            && tag.Category is "subject" or "genre" or "composition" or "lighting" or "palette" or "mood" or "technique"
            && names.Add(tag.Name.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant()));
    }
}
