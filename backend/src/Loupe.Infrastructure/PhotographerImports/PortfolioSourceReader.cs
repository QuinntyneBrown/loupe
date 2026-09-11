using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Loupe.Application.PhotographerImports;
using Loupe.Application.ReferenceImports;
using Loupe.Infrastructure.ReferenceImports;
using Loupe.Domain.Photographers;

namespace Loupe.Infrastructure.PhotographerImports;

public sealed class PortfolioSourceReader(IRestrictedPageFetcher fetcher, IRobotsPolicy robots, TimeProvider clock) : IPortfolioSourceReader
{
    public async Task<PortfolioSourceResult> ReadAsync(string source, CancellationToken cancellationToken)
    {
        using var response = await fetcher.FetchAsync(new Uri(source), cancellationToken, AuthorizeAsync);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.PaymentRequired or HttpStatusCode.Forbidden) throw new SourceImportException("source_access_denied");
        if (!response.IsSuccessStatusCode) throw new SourceImportException((int)response.StatusCode >= 500 || response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ? "source_unavailable" : "source_not_available");
        if (response.Content.Headers.ContentType?.MediaType is not ("text/html" or "application/xhtml+xml")) throw new SourceImportException("unsupported_source");
        var bytes = await BoundedSourceContent.ReadAsync(response.Content, 2_000_000, cancellationToken);
        using var stream = new MemoryStream(bytes);
        using var document = await new HtmlParser().ParseDocumentAsync(stream, cancellationToken);
        if (SourcePageRestrictions.DeniesAccess(document)) throw new SourceImportException("source_access_denied");
        var title = Clean(Meta(document, "og:title") ?? document.Title, 200);
        var description = Clean(Meta(document, "description") ?? Meta(document, "og:description"), 4000);
        var tags = (Meta(document, "keywords") ?? "").Split(',').Select(value => value.Trim().Normalize(NormalizationForm.FormC))
            .Where(value => value.EnumerateRunes().Count() is > 0 and <= 50).DistinctBy(value => value.ToUpperInvariant()).Take(50).ToArray();
        foreach (var excluded in document.QuerySelectorAll("script,style,nav,header,footer,form,noscript,template,[hidden],[aria-hidden='true']")) excluded.Remove();
        var main = document.QuerySelector("main") ?? document.QuerySelector("article") ?? document.Body;
        var text = main is null ? "" : Clean(MainText(main), 20000) ?? "";
        if (title is null && description is null && text.Length == 0) throw new SourceImportException("source_empty");
        return new(new CapturedPortfolioPage(source, response.RequestMessage!.RequestUri!.AbsoluteUri, clock.GetUtcNow(), title, description, text, tags));
    }

    private async Task AuthorizeAsync(Uri source, CancellationToken cancellationToken)
    {
        var decision = await robots.EvaluateAsync(source, cancellationToken);
        if (decision != RobotsDecision.Allowed) throw new SourceImportException(decision == RobotsDecision.Disallowed ? "robots_disallowed" : "robots_unavailable");
    }

    private static string? Meta(IDocument document, string name) => document.QuerySelectorAll("meta")
        .FirstOrDefault(element => string.Equals(element.GetAttribute("name") ?? element.GetAttribute("property"), name, StringComparison.OrdinalIgnoreCase))?.GetAttribute("content");

    private static string MainText(INode root)
    {
        var result = new StringBuilder();
        var pending = new Stack<(INode Node, bool Closing)>(); pending.Push((root, false));
        while (pending.TryPop(out var entry))
        {
            if (entry.Node is IText text) { result.Append(text.Data); continue; }
            var block = entry.Node is IElement element && element.LocalName is "p" or "div" or "section" or "article" or "main" or "br" or "li" or "ul" or "ol" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "table" or "tr" or "td" or "blockquote";
            if (block) result.Append(' ');
            if (entry.Closing) continue;
            pending.Push((entry.Node, true));
            foreach (var child in entry.Node.ChildNodes.Reverse()) pending.Push((child, false));
        }
        return result.ToString();
    }

    private static string? Clean(string? value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = Regex.Replace(value, @"\s+", " ").Trim().Normalize(NormalizationForm.FormC);
        return string.Concat(normalized.EnumerateRunes().Take(maximum).Select(rune => rune.ToString()));
    }
}
