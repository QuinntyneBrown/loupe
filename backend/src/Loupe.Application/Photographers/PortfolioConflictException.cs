namespace Loupe.Application.Photographers;

public sealed class PortfolioConflictException() : Exception("This portfolio is already bookmarked. Use its existing bookmark or enter a different URL.");
