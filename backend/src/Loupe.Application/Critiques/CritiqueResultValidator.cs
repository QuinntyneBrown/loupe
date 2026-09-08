using Loupe.Domain.Critiques;
using Loupe.Domain.Photographs;

namespace Loupe.Application.Critiques;

public static class CritiqueResultValidator
{
    public static bool IsValid(CritiqueResult? result, CaptureMetadata exif)
    {
        if (result is null || result.Strengths is not { Length: > 0 } || result.Improvements is not { Length: 3 }
            || result.Exercise is null || Blank(result.Exercise.Action) || Blank(result.Exercise.Comparison)) return false;
        if (result.Strengths.Any(strength => strength is null || Blank(strength.Explanation)
            || strength.Evidence is not { Length: > 0 } || !ValidEvidence(strength.Evidence, exif))) return false;
        if (result.Improvements.Any(improvement => improvement is null || Blank(improvement.Observation) || Blank(improvement.Effect)
            || Blank(improvement.Action) || improvement.Evidence is not { Length: > 0 } || !ValidEvidence(improvement.Evidence, exif))) return false;
        CritiqueObservation[] observations = [result.Exposure, result.Focus, result.DepthOfField, result.Motion, result.Lighting, result.Color,
            result.Processing, result.Framing, result.SubjectSeparation, result.Balance, result.VisualHierarchy, result.Mood];
        return observations.All(observation => observation is not null && !Blank(observation.Explanation)
            && (observation.Assessable ? observation.Evidence is { Length: > 0 } : !Blank(observation.UncertaintyReason))
            && ValidEvidence(observation.Evidence, exif));
    }

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

    private static bool ValidEvidence(EvidenceStatement[]? evidence, CaptureMetadata exif) => evidence is not null && evidence.All(statement =>
    {
        if (statement is null || Blank(statement.Statement) || !Enum.IsDefined(statement.Kind)) return false;
        if (statement.Kind != EvidenceKind.ExifFact) return statement.ExifField is null && statement.ExifValue is null;
        var supplied = statement.ExifField switch
        {
            "Camera" => exif.Camera,
            "Lens" => exif.Lens,
            "Aperture" => exif.Aperture,
            "ShutterSpeed" => exif.ShutterSpeed,
            "Iso" => exif.Iso,
            "FocalLength" => exif.FocalLength,
            "CapturedAt" => exif.CapturedAt,
            _ => null
        };
        return !Blank(supplied) && supplied == statement.ExifValue;
    });
}
