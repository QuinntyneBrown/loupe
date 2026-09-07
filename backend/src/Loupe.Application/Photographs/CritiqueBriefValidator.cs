using Loupe.Application.Common;
using Loupe.Domain.Photographs;

namespace Loupe.Application.Photographs;

public static class CritiqueBriefValidator
{
    public static CritiqueBrief Normalize(CritiqueBriefInput input) => new()
    {
        Intent = TextField.Normalize(input.Intent, 2000, "intent"),
        Genre = TextField.Normalize(input.Genre, 100, "genre"),
        RequestedFeedback = TextField.Normalize(input.RequestedFeedback, 2000, "requestedFeedback"),
        Experience = input.Experience?.Trim() switch
        {
            null or "" => null,
            "Beginner" => ExperienceLevel.Beginner,
            "Intermediate" => ExperienceLevel.Intermediate,
            "Advanced" => ExperienceLevel.Advanced,
            "Professional" => ExperienceLevel.Professional,
            _ => throw new RequestValidationException("experience", "Choose Beginner, Intermediate, Advanced, or Professional.")
        }
    };
}
