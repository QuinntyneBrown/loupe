using Loupe.Domain.Critiques;

namespace Loupe.Api.Tests.Critiques;

public static class CritiqueResultFixture
{
    public static CritiqueResult Valid()
    {
        var evidence = new[] { new EvidenceStatement(EvidenceKind.VisibleObservation, "The synthetic image has a uniform black field.") };
        var visible = new CritiqueObservation(true, "The uniform black field has no visible tonal variation.", null, evidence);
        var unknown = new CritiqueObservation(false, "Focus cannot be assessed without visible detail.", "The synthetic field contains no textured subject.", []);
        var priority = new Improvement("No subject detail is visible.", "There is no focal point to compare.", "Make a second frame with a lit subject and compare visible detail.", evidence);
        return new CritiqueResult([new CritiqueStrength("The uniform field consistently presents a low-key tone.", evidence)],
            visible, unknown, unknown, unknown, unknown, visible, unknown, unknown, unknown, unknown, unknown, visible,
            [priority, priority with { Action = "Try placing the lit subject near the edge, then compare attention." },
                priority with { Action = "Vary only the background light and compare subject separation." }],
            new PracticeExercise("Photograph a lit subject twice, changing its position.", "Compare which position makes the outline easier to distinguish."));
    }
}
