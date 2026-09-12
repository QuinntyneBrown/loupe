namespace Loupe.Api.Photographers;

public sealed record ReviewPhotographerSuggestionRequest(Guid OperationId, long Revision, string Target, string Decision, string? Name, string? Value, string? Category);
