namespace Loupe.Domain.Photographers;

public sealed record PhotographerSummaryResult(string? Summary, PhotographerSummaryTag[] Tags, string? UnavailableReason);
