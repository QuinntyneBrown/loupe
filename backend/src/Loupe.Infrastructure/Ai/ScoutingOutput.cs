using Loupe.Domain.Scouting;

namespace Loupe.Infrastructure.Ai;

/// <summary>The provider's JSON shape: entries cite images by their 1-based number in the request.</summary>
public sealed record ScoutingOutput(ScoutingOutputStrength[] Overview, ScoutingOutputSuitability[] Suitability, ScoutingOutputTimeOfDay[] TimesOfDay,
    ScoutingOutputTechnique[] Techniques, ScoutingOutputGroupSize GroupSize, ScoutingOutputCaution[] Cautions)
{
    /// <summary>Maps image numbers to the analysed image identifiers; an unknown number becomes an empty identifier the validator rejects.</summary>
    public ScoutingReport ToReport(ScoutingInput input)
    {
        IReadOnlyList<Guid> Cited(int[] numbers) => numbers.Select(number => number >= 1 && number <= input.Images.Count ? input.Images[number - 1].ImageId : Guid.Empty).ToArray();
        return new ScoutingReport(
            Overview.Select(entry => new ReportStrength(entry.Strength, entry.Reason, entry.Basis, Cited(entry.CitedImages))).ToArray(),
            Suitability.Select(entry => new SuitabilityEntry(entry.ShootType, entry.Rating, entry.Reason, entry.Basis, Cited(entry.CitedImages))).ToArray(),
            TimesOfDay.Select(entry => new TimeOfDayEntry(entry.Period, entry.Rating, entry.Reason, entry.Basis, Cited(entry.CitedImages))).ToArray(),
            Techniques.Select(entry => new TechniqueEntry(entry.Technique, entry.Explanation, entry.Basis, Cited(entry.CitedImages))).ToArray(),
            new GroupSizeEntry(GroupSize.CannotAssess, GroupSize.Minimum, GroupSize.Maximum, GroupSize.Reason, GroupSize.Basis, Cited(GroupSize.CitedImages)),
            Cautions.Select(entry => new CautionEntry(entry.Caution, entry.Basis, Cited(entry.CitedImages))).ToArray());
    }
}
