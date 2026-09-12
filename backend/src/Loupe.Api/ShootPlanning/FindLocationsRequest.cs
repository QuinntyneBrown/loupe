using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.ShootPlanning;

public sealed class FindLocationsRequest
{
    [FromQuery(Name = "query")]
    public string? Query { get; init; }
    [FromQuery(Name = "mode")]
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Mode { get; init; } = "keyword";
    [FromQuery(Name = "shootTypes")]
    public string[]? ShootTypes { get; init; }
    [FromQuery(Name = "people")]
    public int? People { get; init; }
    [FromQuery(Name = "timesOfDay")]
    public string[]? TimesOfDay { get; init; }
    [FromQuery(Name = "setting")]
    public string? Setting { get; init; }
    [FromQuery(Name = "tags")]
    public string[]? Tags { get; init; }
    [FromQuery(Name = "pageSize")]
    public int PageSize { get; init; } = 24;
    [FromQuery(Name = "cursor")]
    public string? Cursor { get; init; }
}
