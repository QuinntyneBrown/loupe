using AngleSharp.Html.Parser;
using Loupe.Application.Images;
using Loupe.Application.ReferenceImports;

namespace Loupe.Infrastructure.ReferenceImports;

public sealed class ReferenceSourceReader(IRestrictedPageFetcher fetcher, IRobotsPolicy robots, IImageIngestor ingestor) : IReferenceSourceReader
{
    public async Task<ReferenceSourceResult> ReadAsync(string source, CancellationToken cancellationToken)
    {
        using var response = await fetcher.FetchAsync(new Uri(source), cancellationToken, AuthorizeAsync);
        EnsurePermitted(response);
        var final = response.RequestMessage!.RequestUri!;
        var mediaType = response.Content.Headers.ContentType?.MediaType ?? "";
        if (mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return new(null, null, final.AbsoluteUri, await ImageAsync(response, cancellationToken));
        if (mediaType is not ("text/html" or "application/xhtml+xml")) throw new SourceImportException("unsupported_source");
        var bytes = await BoundedSourceContent.ReadAsync(response.Content, 2_000_000, cancellationToken);
        using var stream = new MemoryStream(bytes);
        using var document = await new HtmlParser().ParseDocumentAsync(stream, cancellationToken);
        if (SourcePageRestrictions.DeniesAccess(document)) throw new SourceImportException("source_access_denied");
        var title = Clean(document.QuerySelector("meta[property='og:title']")?.GetAttribute("content") ?? document.Title, 200);
        var author = Clean(document.QuerySelector("meta[name='author']")?.GetAttribute("content"), 200);
        var candidates = document.QuerySelectorAll("meta[property='og:image'], meta[name='og:image']")
            .Concat(document.QuerySelectorAll("meta[name='twitter:image'], meta[property='twitter:image']"));
        foreach (var candidate in candidates.Take(32))
        {
            var value = candidate.GetAttribute("content");
            if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(final, value, out var imageUri)
                || imageUri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(imageUri.UserInfo) || !imageUri.IsDefaultPort) continue;
            using var imageResponse = await fetcher.FetchAsync(imageUri, cancellationToken, AuthorizeAsync);
            if (imageResponse.StatusCode == System.Net.HttpStatusCode.NotFound) continue;
            EnsurePermitted(imageResponse);
            try { return new(title, author, final.AbsoluteUri, await ImageAsync(imageResponse, cancellationToken)); }
            catch (ImageValidationException) { }
        }
        return new(title, author, final.AbsoluteUri, null, "image_not_found");
    }

    private async Task AuthorizeAsync(Uri source, CancellationToken cancellationToken)
    {
        var decision = await robots.EvaluateAsync(source, cancellationToken);
        if (decision != RobotsDecision.Allowed)
            throw new SourceImportException(decision == RobotsDecision.Disallowed ? "robots_disallowed" : "robots_unavailable");
    }

    private async Task<ProcessedImage> ImageAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        ingestor.Process(await BoundedSourceContent.ReadAsync(response.Content, 25 * 1024 * 1024, cancellationToken),
            response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream", cancellationToken);

    private static void EnsurePermitted(HttpResponseMessage response)
    {
        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.PaymentRequired or System.Net.HttpStatusCode.Forbidden)
            throw new SourceImportException("source_access_denied");
        if (!response.IsSuccessStatusCode)
            throw new SourceImportException((int)response.StatusCode >= 500 || response.StatusCode == System.Net.HttpStatusCode.RequestTimeout ? "source_unavailable" : "source_not_available");
    }

    private static string? Clean(string? value, int limit)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed[..Math.Min(trimmed.Length, limit)];
    }
}
