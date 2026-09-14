using Loupe.Api.ShootPlanning;
using Loupe.Application.Search;
using Loupe.Application.ShootPlanning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/locations/search")]
public sealed class LocationSearchController(ISender sender) : ControllerBase
{
    [HttpGet("tags")]
    public Task<IReadOnlyList<SearchTagCount>> Tags(CancellationToken cancellationToken) => sender.Send(new ListLocationTagsQuery(), cancellationToken);

    [HttpGet]
    public Task<LocationSearchPage> Find([FromQuery] FindLocationsRequest request, CancellationToken cancellationToken) =>
        sender.Send(new FindLocationsQuery(request.Query, request.Mode, request.ShootTypes, request.People, request.TimesOfDay, request.Setting, request.Tags, request.PageSize, request.Cursor), cancellationToken);
}
