namespace Loupe.Domain.Critiques;

public sealed record CritiqueResult(CritiqueStrength[] Strengths,
    CritiqueObservation Exposure, CritiqueObservation Focus, CritiqueObservation DepthOfField,
    CritiqueObservation Motion, CritiqueObservation Lighting, CritiqueObservation Color, CritiqueObservation Processing,
    CritiqueObservation Framing, CritiqueObservation SubjectSeparation, CritiqueObservation Balance,
    CritiqueObservation VisualHierarchy, CritiqueObservation Mood, Improvement[] Improvements, PracticeExercise Exercise);
