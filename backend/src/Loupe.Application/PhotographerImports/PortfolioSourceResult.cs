using Loupe.Domain.Photographers;

namespace Loupe.Application.PhotographerImports;

public sealed record PortfolioSourceResult(CapturedPortfolioPage? Source, string? FailureCode = null);
