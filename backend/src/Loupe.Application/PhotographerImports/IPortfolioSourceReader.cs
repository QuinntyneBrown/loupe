namespace Loupe.Application.PhotographerImports;

public interface IPortfolioSourceReader
{
    Task<PortfolioSourceResult> ReadAsync(string source, CancellationToken cancellationToken);
}
