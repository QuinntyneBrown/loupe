namespace Loupe.Application.ReferenceImports;

public interface IRestrictedPageFetcher
{
    Task<HttpResponseMessage> FetchAsync(Uri source, CancellationToken cancellationToken);
}
