namespace Loupe.Api.Scouting;

public sealed record RequestScoutingReportRequest(long Revision, bool Regenerate = false);
