namespace Loupe.Domain.Photographers;

public sealed record PhotographerSummaryInput(string PortfolioUrl, long SourceRevision, CapturedPortfolioPage? Source);
