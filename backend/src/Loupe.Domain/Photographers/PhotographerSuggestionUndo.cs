namespace Loupe.Domain.Photographers;

public sealed record PhotographerSuggestionUndo(long Revision, string? Summary, string? SummaryProvenance, string SuggestionsJson, string[] TagNames);
