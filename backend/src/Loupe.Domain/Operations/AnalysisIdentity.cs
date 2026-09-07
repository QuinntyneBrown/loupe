namespace Loupe.Domain.Operations;

public sealed record AnalysisIdentity(ExecutionMode Mode, string Model, string PromptVersion);
