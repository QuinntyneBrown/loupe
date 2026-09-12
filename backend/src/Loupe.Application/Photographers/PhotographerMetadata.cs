namespace Loupe.Application.Photographers;

public sealed record PhotographerMetadata(string Name, string PortfolioUrl, string? Summary, string? Notes, PhotographerTagInput[] Tags);
