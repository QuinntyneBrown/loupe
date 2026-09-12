using System.Text;
using Loupe.Application.Common;
using Loupe.Application.References;
using Loupe.Application.Scouting;
using Loupe.Application.Security;
using Loupe.Domain.Locations;
using Loupe.Domain.Scouting;
using MediatR;

namespace Loupe.Application.ShootPlanning;

public sealed class FindLocationsQueryHandler(ICurrentOwner owner, ILocationSearchStore search) : IRequestHandler<FindLocationsQuery, LocationSearchPage>
{
    public async Task<LocationSearchPage> Handle(FindLocationsQuery request, CancellationToken cancellationToken)
    {
        if (request.Mode != "keyword") throw new RequestValidationException("mode", "Choose Keyword or Meaning.");
        if (request.PageSize is < 1 or > 100) throw new RequestValidationException("pageSize", "Choose between 1 and 100 items.");
        if (request.Query?.Contains('\0') == true) throw new RequestValidationException("query", "Remove the null character from the query.");
        var query = TextField.Normalize(request.Query?.Normalize(NormalizationForm.FormC), 500, "query") ?? "";
        var shootTypes = Labels<ShootType>(request.ShootTypes, "shootTypes", "Choose shoot types from the shared list.");
        var periods = Labels<TimeOfDay>(request.TimesOfDay, "timesOfDay", "Choose times of day from the shared list.");
        if (request.People is < 1 or > 500) throw new RequestValidationException("people", "Enter a people count from 1 to 500.");
        var setting = request.Setting is null or "" ? null
            : Enum.TryParse<LocationSetting>(request.Setting, ignoreCase: true, out var parsed) ? parsed.ToString()
            : throw new RequestValidationException("setting", "Choose Indoor, Outdoor, or Mixed.");
        if (request.Tags?.Length > 10 || request.Tags?.Any(tag => tag?.Contains('\0') == true) == true) throw new RequestValidationException("tags", "Choose up to 10 valid tags.");
        var tags = (request.Tags ?? []).Select(tag => TagName.Validate(tag).ToUpperInvariant()).Distinct().Order(StringComparer.Ordinal).ToArray();
        var filter = new LocationSearchFilter(query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries), shootTypes, request.People, periods, setting, tags);
        var scope = LocationSearchCursor.Scope(owner.Id, request.Mode, filter, request.PageSize);
        var cursor = LocationSearchCursor.Parse(request.Cursor, scope);
        var found = await search.ListAsync(owner.Id, filter, request.PageSize + 1, cursor, cancellationToken);
        var items = found.Take(request.PageSize).ToArray();
        var next = found.Count > request.PageSize ? LocationSearchCursor.Encode(new CreatedCursor(items[^1].CreatedAt, items[^1].Id), scope) : null;
        return new LocationSearchPage(items, next, await search.CountAsync(owner.Id, filter, cancellationToken));
    }

    // Filters travel as the shared labels ("Golden hour"), validated against the enumeration's labels.
    private static string[] Labels<T>(string[]? values, string field, string message) where T : struct, Enum
    {
        var labels = ScoutingVocabulary.Labels<T>();
        var chosen = (values ?? []).Select(value => value?.Trim() ?? "").Where(value => value.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        if (chosen.Length > labels.Count || chosen.Any(value => !labels.Contains(value))) throw new RequestValidationException(field, message);
        return chosen;
    }
}
