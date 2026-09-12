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
        var details = new LocationDetailsInput(request.Name, request.AddressLine1, request.AddressLine2, request.Locality, request.Region,
            request.PostalCode, request.Country, request.Coordinates, request.Setting);
        var result = await sender.Send(new CreateLocationCommand(details, request.ScoutingBrief, request.Notes, request.Tags, operationKey), cancellationToken);
        return Created($"/api/locations/{result.Id}", result);
    }

    [HttpGet]
    public Task<LocationPage> List(CancellationToken cancellationToken, [FromQuery] int pageSize = 24, [FromQuery] string? cursor = null) =>
        sender.Send(new ListLocationsQuery(pageSize, cursor), cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<LocationResult> Get(Guid id, CancellationToken cancellationToken) => sender.Send(new GetLocationQuery(id), cancellationToken);

    [HttpPut("{id:guid}")]
    public Task<LocationResult> Update(Guid id, UpdateLocationRequest request, CancellationToken cancellationToken) =>
        sender.Send(new UpdateLocationCommand(id, request.Revision, new LocationDetailsInput(request.Name, request.AddressLine1, request.AddressLine2,
            request.Locality, request.Region, request.PostalCode, request.Country, request.Coordinates, request.Setting)), cancellationToken);

    [HttpPut("{id:guid}/scouting-brief")]
    public Task<LocationResult> ScoutingBrief(Guid id, UpdateLocationTextRequest request, CancellationToken cancellationToken) =>
        sender.Send(new UpdateLocationTextCommand(id, request.Revision, LocationTextField.ScoutingBrief, request.Text), cancellationToken);

    [HttpPut("{id:guid}/notes")]
    public Task<LocationResult> Notes(Guid id, UpdateLocationTextRequest request, CancellationToken cancellationToken) =>
        sender.Send(new UpdateLocationTextCommand(id, request.Revision, LocationTextField.Notes, request.Text), cancellationToken);

    [HttpPut("{id:guid}/tags")]
    public Task<LocationResult> Tags(Guid id, SetLocationTagsRequest request, CancellationToken cancellationToken) =>
        sender.Send(new SetLocationTagsCommand(id, request.Revision, request.Tags), cancellationToken);
}
