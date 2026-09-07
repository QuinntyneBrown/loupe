using Loupe.Api.Photographs;
using Loupe.Application.Images;
using Loupe.Application.Photographs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/photographs")]
public sealed class PhotographsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(UploadLimits.RequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimits.RequestBytes)]
    public async Task<ActionResult<PhotographResult>> Upload([FromForm] UploadPhotographRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UploadPhotographCommand(new ImageUpload(request.Image.OpenReadStream,
            request.Image.FileName, request.Image.ContentType, request.Image.Length), request.Title,
            new CritiqueBriefInput(request.Intent, request.Genre, request.Experience, request.RequestedFeedback)), cancellationToken);
        return Created($"/api/photographs/{result.Id}", result);
    }

    [HttpGet("{id:guid}")]
    public Task<PhotographResult> Get(Guid id, CancellationToken cancellationToken) => sender.Send(new GetPhotographQuery(id), cancellationToken);

    [HttpGet("{id:guid}/image")]
    public async Task<IActionResult> Image(Guid id, CancellationToken cancellationToken)
    {
        var image = await sender.Send(new GetPhotographImageQuery(id, false), cancellationToken);
        return File(image.Content, image.ContentType);
    }

    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, CancellationToken cancellationToken)
    {
        var image = await sender.Send(new GetPhotographImageQuery(id, true), cancellationToken);
        return File(image.Content, image.ContentType);
    }
}
