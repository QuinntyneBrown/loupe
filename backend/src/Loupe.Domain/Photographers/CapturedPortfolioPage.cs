namespace Loupe.Domain.Photographers;

public sealed record CapturedPortfolioPage(string RequestedUrl, string FetchedUrl, DateTimeOffset RetrievedAt,
    string? Title, string? Description, string MainText, string[] Tags);
