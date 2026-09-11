namespace Loupe.Application.Photographers;

public interface IPortfolioUrlPolicy
{
    bool IsAllowed(Uri source);
}
