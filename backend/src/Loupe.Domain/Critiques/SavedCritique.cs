using Loupe.Domain.Operations;
using Loupe.Domain.Photographs;

namespace Loupe.Domain.Critiques;

public sealed record SavedCritique(Guid OperationId, DateTimeOffset GeneratedAt, ExecutionMode Mode,
    string Model, string PromptVersion, CritiqueBrief Brief, CritiqueResult Content);
