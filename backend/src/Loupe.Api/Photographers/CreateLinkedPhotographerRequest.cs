namespace Loupe.Api.Photographers;

public sealed record CreateLinkedPhotographerRequest(long Revision, string? Name, string? PortfolioUrl);
