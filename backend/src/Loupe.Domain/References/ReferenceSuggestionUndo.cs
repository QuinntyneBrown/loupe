namespace Loupe.Domain.References;

public sealed record ReferenceSuggestionUndo(long Revision, string? Description, string? DescriptionProvenance,
    string SuggestionsJson, ReferenceTagSnapshot[] Tags);
