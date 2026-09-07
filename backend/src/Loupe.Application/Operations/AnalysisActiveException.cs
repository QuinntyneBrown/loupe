namespace Loupe.Application.Operations;

public sealed class AnalysisActiveException() : Exception("Let the current analysis finish before requesting different inputs.");
