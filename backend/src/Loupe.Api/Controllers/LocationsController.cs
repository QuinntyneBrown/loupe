using Loupe.Api.Locations;
using Loupe.Application.Images;
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

    [HttpPost("{id:guid}/images")]
    [RequestSizeLimit(UploadLimits.RequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimits.RequestBytes)]
    public async Task<IActionResult> AddImage(Guid id, [FromForm] AddLocationImageRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? operationKey, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AddLocationImageCommand(id, new ImageUpload(request.Image.OpenReadStream,
            request.Image.FileName, request.Image.ContentType, request.Image.Length), operationKey, Request.Form.Files.Count), cancellationToken);
        return Created($"/api/locations/{id}", result);
    }

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    public Task<LocationResult> RemoveImage(Guid id, Guid imageId, [FromQuery] long revision, CancellationToken cancellationToken) =>
        sender.Send(new RemoveLocationImageCommand(id, imageId, revision), cancellationToken);

    [HttpPut("{id:guid}/cover")]
    public Task<LocationResult> Cover(Guid id, SetLocationCoverRequest request, CancellationToken cancellationToken) =>
        sender.Send(new SetLocationCoverCommand(id, request.Revision, request.ImageId), cancellationToken);

    [HttpGet("{id:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> Image(Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        var image = await sender.Send(new GetLocationImageQuery(id, imageId, false), cancellationToken);
        return File(image.Content, image.ContentType);
    }

    [HttpGet("{id:guid}/images/{imageId:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        var image = await sender.Send(new GetLocationImageQuery(id, imageId, true), cancellationToken);
        return File(image.Content, image.ContentType);
    }
}
