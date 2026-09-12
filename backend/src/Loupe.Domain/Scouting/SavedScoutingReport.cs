using Loupe.Domain.Operations;

namespace Loupe.Domain.Scouting;

public sealed record SavedScoutingReport(Guid OperationId, DateTimeOffset GeneratedAt, ExecutionMode Mode, string Model, string PromptVersion,
    string? BriefSnapshot, long ImageSetRevision, int ImageCount, ScoutingReport Report);
