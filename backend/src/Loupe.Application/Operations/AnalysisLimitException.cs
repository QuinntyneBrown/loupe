namespace Loupe.Application.Operations;

public sealed class AnalysisLimitException() : Exception("Five analyses are already active. Wait for one to finish and try again.");
