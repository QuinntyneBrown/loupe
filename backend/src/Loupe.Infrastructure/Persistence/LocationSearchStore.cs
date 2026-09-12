using Loupe.Application.Common;
using Loupe.Application.Locations;
using Loupe.Application.Scouting;
using Loupe.Application.ShootPlanning;
using Loupe.Domain.Operations;
using Loupe.Domain.Scouting;
using Microsoft.EntityFrameworkCore;

namespace Loupe.Infrastructure.Persistence;

public sealed class LocationSearchStore(LibraryDbContext database) : ILocationSearchStore
{
    public Task<int> CountAsync(string ownerId, LocationSearchFilter filter, CancellationToken cancellationToken) =>
        Filtered(ownerId, filter).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<LocationSearchItem>> ListAsync(string ownerId, LocationSearchFilter filter, int count, CreatedCursor? cursor, CancellationToken cancellationToken)
    {
        var query = Filtered(ownerId, filter);
        if (cursor is not null) query = query.Where(row => row.CreatedAt < cursor.CreatedAt || row.CreatedAt == cursor.CreatedAt && row.Id.CompareTo(cursor.Id) > 0);
        var rows = await query.OrderByDescending(row => row.CreatedAt).ThenBy(row => row.Id).Take(count).ToListAsync(cancellationToken);
        return rows.Select(Item).ToArray();
    }

    private static LocationSearchItem Item(LocationSearchRow row)
    {
        var saved = ScoutingReportJson.Deserialize(row.ScoutingReportJson);
        var status = row.OperationStatus is null ? (OperationStatus?)null : Enum.Parse<OperationStatus>(row.OperationStatus);
        var report = saved?.Report;
        return new LocationSearchItem(row.Id, row.Name, row.Locality,
            row.CoverImageId is { } cover ? LocationImageUrls.Preview(row.Id, cover) : null, row.ImageCount,
            LocationReportStatus.Derive(status, saved, row.ImageSetRevision),
            report?.TimesOfDay.Where(entry => entry.Rating == TimeOfDayRating.Recommended).Select(entry => Label(entry.Period)).ToArray() ?? [],
            report is null ? null : new LocationSearchGroupSize(report.GroupSize.CannotAssess, report.GroupSize.Minimum, report.GroupSize.Maximum),
            report?.Suitability.Select(entry => new LocationSearchSuitability(Label(entry.ShootType), Label(entry.Rating))).ToArray() ?? [],
            row.CreatedAt);
    }

    private static string Label<T>(T value) where T : struct, Enum => ScoutingVocabulary.Labels<T>()[Array.IndexOf(Enum.GetValues<T>(), value)];

    // Keyword text: every eligible field plus every string value of the current report; filters read the report JSON's shared labels.
    private IQueryable<LocationSearchRow> Filtered(string ownerId, LocationSearchFilter filter) => database.Database.SqlQuery<LocationSearchRow>($$"""
        SELECT l."Id", l."Name", l."Locality", l."CoverImageId",
            (SELECT count(*)::integer FROM location_images i WHERE i."LocationId" = l."Id" AND i."OwnerId" = {{ownerId}}) AS "ImageCount",
            (SELECT o."Status" FROM background_operations o WHERE o."Id" = l."CurrentScoutingOperationId") AS "OperationStatus",
            l."ScoutingReportJson"::text AS "ScoutingReportJson", l."ImageSetRevision", l."CreatedAt"
        FROM locations l
        WHERE l."OwnerId" = {{ownerId}}
            AND (CAST({{filter.Setting}} AS text) IS NULL OR l."Setting" = CAST({{filter.Setting}} AS text))
            AND ARRAY(SELECT t."NormalizedName" FROM location_tags t WHERE t."LocationId" = l."Id" AND t."OwnerId" = {{ownerId}}) @> {{filter.Tags}}
            AND (NOT CAST({{filter.RequiresReport}} AS boolean) OR l."ScoutingReportJson" IS NOT NULL)
            AND NOT EXISTS (SELECT 1 FROM unnest({{filter.ShootTypes}}) chosen WHERE NOT EXISTS (
                SELECT 1 FROM jsonb_array_elements(l."ScoutingReportJson"->'Report'->'Suitability') s
                WHERE s->>'ShootType' = chosen AND s->>'Rating' IN ('Well suited', 'Workable')))
            AND (CAST({{filter.People}} AS integer) IS NULL OR (
                (l."ScoutingReportJson"->'Report'->'GroupSize'->>'CannotAssess')::boolean = false
                AND (l."ScoutingReportJson"->'Report'->'GroupSize'->>'Minimum')::integer <= CAST({{filter.People}} AS integer)
                AND (l."ScoutingReportJson"->'Report'->'GroupSize'->>'Maximum')::integer >= CAST({{filter.People}} AS integer)))
            AND (cardinality({{filter.TimesOfDay}}) = 0 OR EXISTS (
                SELECT 1 FROM jsonb_array_elements(l."ScoutingReportJson"->'Report'->'TimesOfDay') p
                WHERE p->>'Period' = ANY({{filter.TimesOfDay}}) AND p->>'Rating' = 'Recommended'))
            AND NOT EXISTS (SELECT 1 FROM unnest({{filter.Tokens}}) token WHERE strpos(upper(normalize(
                concat_ws(E'\n', l."Name", l."AddressLine1", l."AddressLine2", l."Locality", l."Region", l."PostalCode", l."Country", l."Setting", l."Notes", l."ScoutingBrief",
                    (SELECT string_agg(t."Name", E'\n') FROM location_tags t WHERE t."LocationId" = l."Id" AND t."OwnerId" = {{ownerId}}),
                    (SELECT string_agg(v #>> '{}', E'\n') FROM jsonb_path_query(coalesce(l."ScoutingReportJson", '{}'::jsonb),
                        '$.Report.** ? (@.type() == "string")') v)),
                NFC)), upper(token)) = 0)
        """);
}
