namespace Loupe.Api.Photographs;

public sealed record UpdateBriefRequest(long Revision, string? Intent, string? Genre, string? Experience, string? RequestedFeedback);
