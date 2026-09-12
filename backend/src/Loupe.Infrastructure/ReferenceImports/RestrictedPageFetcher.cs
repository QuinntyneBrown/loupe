using System.Net;
using Loupe.Application.ReferenceImports;
using Microsoft.Extensions.DependencyInjection;

namespace Loupe.Infrastructure.ReferenceImports;

/// <summary>
/// Fetches a permitted public source over the named "sourceFetch" HttpClient, whose
/// <see cref="System.Net.Http.SocketsHttpHandler.ConnectCallback"/> (registered in
/// <c>PersistenceSetup</c>) resolves and validates every connection against
/// <see cref="PublicAddressPolicy"/> before it opens, so DNS validation applies to the
/// actual connected address rather than a separately re-resolved one. Redirects are
/// followed manually (autoredirect disabled on the handler) so each hop is re-validated
/// by the same syntax check and connect-time policy gate. See L2-040.
/// </summary>
public sealed class RestrictedPageFetcher(IHttpClientFactory clients) : IRestrictedPageFetcher
{
    private const int MaxRedirects = 5;

    public async Task<HttpResponseMessage> FetchAsync(Uri source, CancellationToken cancellationToken, Func<Uri, CancellationToken, Task>? authorize = null)
    {
        var current = source;
        for (var hop = 0; ; hop++)
        {
            ValidateSyntax(current);
            if (hop > MaxRedirects) throw new SourceFetchException(SourceFetchFailureKind.RedirectLimitExceeded);
            if (authorize is not null) await authorize(current, cancellationToken);
            using var client = clients.CreateClient("sourceFetch");
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            HttpResponseMessage response;
            try { response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken); }
            catch (HttpRequestException failure) when (failure.InnerException is SourceFetchException rejection) { throw rejection; }
            if (IsRedirect(response.StatusCode) && response.Headers.Location is { } location)
            {
                response.Dispose();
                current = location.IsAbsoluteUri ? location : new Uri(current, location);
                continue;
            }
            return response;
        }
    }

    private static bool IsRedirect(HttpStatusCode status) => status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found
        or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static void ValidateSyntax(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new SourceFetchException(SourceFetchFailureKind.ForbiddenDestination);
        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new SourceFetchException(SourceFetchFailureKind.ForbiddenDestination);
        if (uri.Port != (uri.Scheme == Uri.UriSchemeHttp ? 80 : 443))
            throw new SourceFetchException(SourceFetchFailureKind.ForbiddenDestination);
    }
}
