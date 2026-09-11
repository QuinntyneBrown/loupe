using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Loupe.Application.Boards;
using Loupe.Application.Common;
using Loupe.Application.References;
using Loupe.Application.Security;
using MediatR;

namespace Loupe.Application.Search;

public sealed class SearchQueryHandler(ICurrentOwner owner, ISearchStore search, IBoardStore boards) : IRequestHandler<SearchQuery, SearchPage>
{
    public async Task<SearchPage> Handle(SearchQuery request, CancellationToken cancellationToken)
    {
        if (request.PageSize is < 1 or > 100) throw new RequestValidationException("pageSize", "Choose between 1 and 100 items.");
        if (request.Type is not ("all" or "references" or "photographers")) throw new RequestValidationException("type", "Choose All, References or Photographers.");
        if (request.Query?.Contains('\0') == true) throw new RequestValidationException("query", "Remove the null character from the query.");
        var query = TextField.Normalize(request.Query?.Normalize(NormalizationForm.FormC), 500, "query") ?? "";
        if (request.Tags?.Length > 10 || request.Tags?.Any(tag => tag.Contains('\0')) == true) throw new RequestValidationException("tags", "Choose up to 10 valid tags.");
        if (request.BoardIds?.Length > 10) throw new RequestValidationException("boardIds", "Choose up to 10 boards.");
        var tags = (request.Tags ?? []).Select(tag => TagName.Validate(tag).ToUpperInvariant()).Distinct().Order(StringComparer.Ordinal).ToArray();
        var boardIds = (request.BoardIds ?? []).Distinct().Order().ToArray();
        foreach (var boardId in boardIds) await boards.RequireOwnedAsync(owner.Id, boardId, cancellationToken);
        var filter = new SearchFilter(query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries), request.Type, tags, boardIds);
        var scope = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { owner.Id, filter, request.PageSize }))) + ".";
        if (request.Cursor is not null && (request.Cursor.Length > 200 || !request.Cursor.StartsWith(scope, StringComparison.Ordinal)))
            throw new RequestValidationException("cursor", "This cursor belongs to a different search. Refresh the results.");
        var cursor = CreatedCursor.Parse(request.Cursor?[scope.Length..]);
        var found = await search.ListAsync(owner.Id, filter, request.PageSize + 1, cursor, cancellationToken);
        var items = found.Take(request.PageSize).ToArray();
        var next = found.Count > request.PageSize ? scope + new CreatedCursor(items[^1].CreatedAt, items[^1].Id).Encode() : null;
        return new(items, next, await search.CountAsync(owner.Id, filter, cancellationToken));
    }
}
