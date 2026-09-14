using System.Text;
using Loupe.Application.Common;
using Loupe.Application.References;
using Loupe.Application.Operations;
using Loupe.Application.Scouting;
using Loupe.Application.Search;
using Loupe.Application.Security;
using Loupe.Domain.Locations;
using Loupe.Domain.Scouting;
using MediatR;

namespace Loupe.Application.ShootPlanning;

public sealed class FindLocationsQueryHandler(ICurrentOwner owner, ILocationSearchStore search, IEmbeddingConfiguration configuration, IEmbeddingProvider embeddings)
    : IRequestHandler<FindLocationsQuery, LocationSearchPage>
{
    public async Task<LocationSearchPage> Handle(FindLocationsQuery request, CancellationToken cancellationToken)
    {
        if (request.Mode is not ("keyword" or "meaning")) throw new RequestValidationException("mode", "Choose Keyword or Meaning.");
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
        // Meaning ranks the whole query as one vector; only Keyword matches its tokens.
        var tokens = request.Mode == "keyword" ? query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries) : [];
        var filter = new LocationSearchFilter(tokens, shootTypes, request.People, periods, setting, tags);
        var scope = LocationSearchCursor.Scope(owner.Id, request.Mode, filter, request.PageSize);
        if (request.Mode == "meaning") return await RankAsync(query, filter, scope, request, cancellationToken);
        var cursor = LocationSearchCursor.Parse(request.Cursor, scope);
        var found = await search.ListAsync(owner.Id, filter, request.PageSize + 1, cursor, cancellationToken);
        var items = found.Take(request.PageSize).ToArray();
        var next = found.Count > request.PageSize ? LocationSearchCursor.Encode(new CreatedCursor(items[^1].CreatedAt, items[^1].Id), scope) : null;
        return new LocationSearchPage(items, next, await search.CountAsync(owner.Id, filter, cancellationToken));
    }

    // Meaning: a blank query is refused before any embedding; the query vector is compared only with vectors of the configured model.
    private async Task<LocationSearchPage> RankAsync(string query, LocationSearchFilter filter, string scope, FindLocationsQuery request, CancellationToken cancellationToken)
    {
        if (query.Length == 0) throw new RequestValidationException("query", "Describe the shoot to search by meaning.");
        if (!configuration.IsConfigured) throw new SearchUnavailableException();
        var cursor = SemanticCursor.Parse(request.Cursor, scope, configuration.Model);
        float[] vector;
        try { vector = await embeddings.EmbedAsync(query, cancellationToken); }
        catch (Exception exception) when (exception is ProviderFailureException or IntegrationNotConfiguredException) { throw new SearchUnavailableException(); }
        var ranking = new LocationSearchRanking(configuration.Model, vector);
        var found = await search.RankAsync(owner.Id, filter, ranking, request.PageSize + 1, cursor, cancellationToken);
        var items = found.Take(request.PageSize).ToArray();
        var next = found.Count > request.PageSize ? new SemanticCursor(items[^1].Score!.Value, items[^1].Id).Encode(scope, configuration.Model) : null;
        return new LocationSearchPage(items, next, await search.CountRankedAsync(owner.Id, filter, ranking, cancellationToken));
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
