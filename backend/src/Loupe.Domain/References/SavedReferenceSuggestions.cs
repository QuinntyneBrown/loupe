using Loupe.Domain.Operations;

namespace Loupe.Domain.References;

public sealed record SavedReferenceSuggestions(Guid OperationId, long ImageRevision, DateTimeOffset CreatedAt,
    ExecutionMode Mode, string Model, string PromptVersion, string? Description, string DescriptionState, ReferenceSuggestedTag[] Tags);
