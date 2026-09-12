using Loupe.Application.Photographers;

namespace Loupe.Api.Photographers;

public sealed record SavePhotographerRequest(string? Name, string? PortfolioUrl, string? Summary, string? Notes, PhotographerTagInput[]? Tags);
