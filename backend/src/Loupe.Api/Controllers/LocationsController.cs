using Loupe.Api.Locations;
using Loupe.Application.Locations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/locations")]
public sealed class LocationsController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateLocationRequest request, [FromHeader(Name = "Idempotency-Key")] string? operationKey, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateLocationCommand(request.Name, request.AddressLine1, request.AddressLine2, request.Locality, request.Region,
            request.PostalCode, request.Country, request.Coordinates, request.Setting, request.ScoutingBrief, request.Notes, request.Tags, operationKey), cancellationToken);
        return Created($"/api/locations/{result.Id}", result);
    }

    [HttpGet("{id:guid}")]
    public Task<LocationResult> Get(Guid id, CancellationToken cancellationToken) => sender.Send(new GetLocationQuery(id), cancellationToken);
}
