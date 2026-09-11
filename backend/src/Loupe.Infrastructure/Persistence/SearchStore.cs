using Loupe.Application.Common;
using Loupe.Application.Search;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class SearchStore(LibraryDbContext database) : ISearchStore
{
    public Task<int> CountAsync(string ownerId, SearchFilter filter, CancellationToken cancellationToken) =>
        Filtered(ownerId, filter).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<SearchItem>> ListAsync(string ownerId, SearchFilter filter, int count, CreatedCursor? cursor, CancellationToken cancellationToken)
    {
        var query = Filtered(ownerId, filter);
        if (cursor is not null) query = query.Where(item => item.CreatedAt < cursor.CreatedAt || item.CreatedAt == cursor.CreatedAt && item.Id.CompareTo(cursor.Id) > 0);
        return await query.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id).Take(count).ToListAsync(cancellationToken);
    }

    private IQueryable<SearchItem> Filtered(string ownerId, SearchFilter filter) => database.Database.SqlQuery<SearchItem>($"""
        SELECT library."Id", library."Type", library."Title", library."CreatedAt", library."PreviewUrl", library."SourceUrl",
            library."Attribution", library."Description", library."Width", library."Height"
        FROM (
            SELECT r."Id", 'reference' AS "Type", r."Title", r."CreatedAt",
                CASE WHEN r."PreviewKey" IS NULL THEN NULL ELSE '/api/references/' || r."Id" || '/preview?v=' || r."ImageRevision" END AS "PreviewUrl",
                r."SourceUrl", r."Attribution", r."Description", r."Width", r."Height",
                concat_ws(E'\n', r."Title", r."Description", r."Notes", r."Attribution", p."Name",
                    substring(r."SourceUrl" from '^https?://(\[[^]]+\]|[^/:?#]+)'),
                    (SELECT string_agg(t."Name", E'\n') FROM reference_tags t WHERE t."ReferenceId" = r."Id" AND t."OwnerId" = {ownerId})) AS text,
                ARRAY(SELECT t."NormalizedName" FROM reference_tags t WHERE t."ReferenceId" = r."Id" AND t."OwnerId" = {ownerId}) AS tags
            FROM "references" r LEFT JOIN photographers p ON p."Id" = r."PhotographerId" AND p."OwnerId" = r."OwnerId"
            WHERE r."OwnerId" = {ownerId} AND {filter.Type} IN ('all', 'references')
                AND (cardinality({filter.BoardIds}) = 0 OR EXISTS (SELECT 1 FROM board_references b WHERE b."ReferenceId" = r."Id" AND b."OwnerId" = {ownerId} AND b."BoardId" = ANY({filter.BoardIds})))
            UNION ALL
            SELECT p."Id", 'photographer', p."Name", p."CreatedAt", NULL, p."PortfolioUrl", NULL, p."Summary", NULL::integer, NULL::integer,
                concat_ws(E'\n', p."Name", p."Summary", p."Notes", substring(p."PortfolioUrl" from '^https?://(\[[^]]+\]|[^/:?#]+)'),
                    (SELECT string_agg(t."Name", E'\n') FROM photographer_tags t WHERE t."PhotographerId" = p."Id" AND t."OwnerId" = {ownerId})),
                ARRAY(SELECT t."NormalizedName" FROM photographer_tags t WHERE t."PhotographerId" = p."Id" AND t."OwnerId" = {ownerId})
            FROM photographers p WHERE p."OwnerId" = {ownerId} AND {filter.Type} IN ('all', 'photographers') AND cardinality({filter.BoardIds}) = 0
        ) library
        WHERE library.tags @> {filter.Tags}
            AND NOT EXISTS (SELECT 1 FROM unnest({filter.Tokens}) token WHERE strpos(upper(normalize(library.text, NFC)), upper(token)) = 0)
        """);
}
