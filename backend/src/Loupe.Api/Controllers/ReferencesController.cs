using Loupe.Api.References;
using Loupe.Application.Images;
using Loupe.Application.References;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/references")]
public sealed class ReferencesController(ISender sender) : ControllerBase
{
    [HttpPost("images")]
    [RequestSizeLimit(UploadLimits.RequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimits.RequestBytes)]
    public async Task<ActionResult<ReferenceResult>> Upload([FromForm] UploadReferenceRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? operationKey, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UploadReferenceCommand(new ImageUpload(request.Image.OpenReadStream,
            request.Image.FileName, request.Image.ContentType, request.Image.Length), request.Title,
            request.SourceUrl, request.Attribution, request.Notes, operationKey, Request.Form.Files.Count), cancellationToken);
        return Created($"/api/references/{result.Id}", result);
    }
    [HttpGet("{id:guid}")]
    public Task<ReferenceResult> Get(Guid id, CancellationToken cancellationToken) => sender.Send(new GetReferenceQuery(id), cancellationToken);

    [HttpGet("{id:guid}/image")]
    public async Task<IActionResult> Image(Guid id, CancellationToken cancellationToken)
    {
        var image = await sender.Send(new GetReferenceImageQuery(id, false), cancellationToken);
        return File(image.Content, image.ContentType);
    }
    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, CancellationToken cancellationToken)
    {
        var image = await sender.Send(new GetReferenceImageQuery(id, true), cancellationToken);
        return File(image.Content, image.ContentType);
    }
}
