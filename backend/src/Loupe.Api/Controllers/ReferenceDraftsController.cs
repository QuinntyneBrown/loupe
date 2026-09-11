using Loupe.Api.ReferenceDrafts;
using Loupe.Application.Images;
using Loupe.Application.ReferenceDrafts;
using Loupe.Application.References;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reference-drafts")]
public sealed class ReferenceDraftsController(ISender sender) : ControllerBase
{
    [HttpPost("images")]
    [RequestSizeLimit(UploadLimits.RequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimits.RequestBytes)]
    public async Task<ActionResult<ReferenceDraftResult>> Upload([FromForm] UploadReferenceDraftRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? operationKey, CancellationToken cancellationToken)
    {
        var draft = await sender.Send(new UploadReferenceDraftCommand(new ImageUpload(request.Image.OpenReadStream, request.Image.FileName,
            request.Image.ContentType, request.Image.Length), request.SourceUrl, operationKey, Request.Form.Files.Count), cancellationToken);
        return Created($"/api/reference-drafts/{draft.Id}", draft);
    }
    [HttpGet("{id:guid}")]
    public Task<ReferenceDraftResult> Get(Guid id, CancellationToken cancellationToken) => sender.Send(new GetReferenceDraftQuery(id), cancellationToken);
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteReferenceDraftCommand(id), cancellationToken);
        return NoContent();
    }
    [HttpPost("{id:guid}/save")]
    public Task<SaveReferenceUrlResult> Save(Guid id, SaveReferenceDraftRequest request, [FromHeader(Name = "Idempotency-Key")] string? operationKey, CancellationToken cancellationToken) =>
        sender.Send(new SaveReferenceDraftCommand(id, request.Revision, request.Title, request.SourceUrl, request.Attribution, request.Notes, request.BoardIds, operationKey), cancellationToken);
    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, CancellationToken cancellationToken)
    {
        var image = await sender.Send(new GetReferenceDraftImageQuery(id, true), cancellationToken);
        return File(image.Content, image.ContentType);
    }
    [HttpGet("{id:guid}/image")]
    public async Task<IActionResult> Image(Guid id, CancellationToken cancellationToken)
    {
        var image = await sender.Send(new GetReferenceDraftImageQuery(id, false), cancellationToken);
        return File(image.Content, image.ContentType);
    }
}
