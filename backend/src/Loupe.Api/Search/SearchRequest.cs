using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Search;

public sealed class SearchRequest
{
    [FromQuery(Name = "query")]
    public string? Query { get; init; }
    [FromQuery(Name = "type")]
    public string Type { get; init; } = "all";
    [FromQuery(Name = "tags")]
    public string[]? Tags { get; init; }
    [FromQuery(Name = "boardIds")]
    public Guid[]? BoardIds { get; init; }
    [FromQuery(Name = "pageSize")]
    public int PageSize { get; init; } = 24;
    [FromQuery(Name = "cursor")]
    public string? Cursor { get; init; }

    [FromQuery(Name = "mode")]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Mode { get; init; } = "keyword";
}
