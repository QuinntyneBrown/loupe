namespace Loupe.Api.References;

public sealed record ReviewReferenceSuggestionRequest(Guid OperationId, long Revision, string Target, string Decision, string? Name, string? Value, string? Category);
