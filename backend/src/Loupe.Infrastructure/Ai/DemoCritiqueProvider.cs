using Loupe.Application.Critiques;
using Loupe.Application.Common;
using Loupe.Domain.Critiques;
using Loupe.Domain.Operations;

namespace Loupe.Infrastructure.Ai;

public sealed class DemoCritiqueProvider : ICritiqueProvider
{
    public Task<CritiqueResult> GenerateAsync(CritiqueInput input, AnalysisIdentity identity, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (identity.Mode != ExecutionMode.Demo) throw new IntegrationNotConfiguredException();
        var evidence = new[] { new EvidenceStatement(EvidenceKind.StylisticPreference,
            "Demo sample guidance for practice; this is not an assessment of the uploaded photograph.") };
        var unknown = new CritiqueObservation(false, "Demo mode provides sample guidance without image analysis.",
            "This aspect cannot be assessed by the deterministic Demo provider.", []);
        var strengths = new[] { new CritiqueStrength("Demo example: a clear intended mood can guide consistent choices of framing, light and timing.", evidence) };
        var improvements = new[]
        {
            new Improvement("Demo example: a bright edge may compete with the subject.", "It can draw attention away from the intended focal point.",
                "For an optional framing study, make a second exposure with that edge outside the frame and compare where your eye lands first.", evidence),
            new Improvement("Demo example: an overlapping background shape may merge with the subject.", "It can make the silhouette harder to read.",
                "Try a small sideways camera move while keeping your intended mood, then compare the subject outline.", evidence),
            new Improvement("Demo example: different movement treatments can communicate different moods.", "The preferred rendering depends on your intent; blur is not inherently an error.",
                "Keep one frame that follows your intention and make one optional alternative with a different motion treatment.", evidence)
        };
        var exercise = new PracticeExercise("Make two photographs of the same subject, keeping your stated intent and changing only camera position.",
            "Compare which frame directs attention to your intended subject more clearly and record the visible difference.");
        return Task.FromResult(new CritiqueResult(strengths, unknown, unknown, unknown, unknown, unknown, unknown,
            unknown, unknown, unknown, unknown, unknown, unknown, improvements, exercise));
    }
}
