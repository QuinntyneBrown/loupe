namespace Loupe.Application.Operations;

public sealed class AnalysisInputsChangedException() : Exception("The analysis inputs changed. Request a new critique using the current photograph and brief.");
