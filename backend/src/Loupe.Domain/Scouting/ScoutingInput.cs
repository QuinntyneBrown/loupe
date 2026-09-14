namespace Loupe.Domain.Scouting;

public sealed record ScoutingInput(long ImageSetRevision, string? ScoutingBrief, IReadOnlyList<ScoutingImageInput> Images);
