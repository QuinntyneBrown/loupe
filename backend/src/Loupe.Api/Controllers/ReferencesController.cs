using Loupe.Api.References;
using Loupe.Api.Boards;
using Loupe.Application.Boards;
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
    [HttpPut("{id:guid}/tags")]
    public Task<ReferenceResult> SetTags(Guid id, SetReferenceTagsRequest request, CancellationToken cancellationToken) =>
        sender.Send(new SetReferenceTagsCommand(id, request.Revision, request.Tags), cancellationToken);
    [HttpPut("{id:guid}/boards")]
    public Task<ReferenceResult> SetBoards(Guid id, SetReferenceBoardsRequest request, CancellationToken cancellationToken) =>
        sender.Send(new SetReferenceBoardsCommand(id, request.Revision, request.BoardIds), cancellationToken);
    [HttpPost("links")]
    public async Task<ActionResult<SaveReferenceUrlResult>> SaveLink(SaveReferenceUrlRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? operationKey, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SaveReferenceUrlCommand(request.SourceUrl, request.Title, request.Attribution, request.Notes, operationKey), cancellationToken);
        return result.AlreadySaved ? Ok(result) : Created($"/api/references/{result.Reference.Id}", result);
    }

    [HttpGet]
    public Task<ReferencePage> List(CancellationToken cancellationToken, [FromQuery] int pageSize = 24, [FromQuery] string? cursor = null, [FromQuery] Guid? boardId = null) =>
        sender.Send(new ListReferencesQuery(pageSize, cursor, boardId), cancellationToken);

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

    [HttpPut("{id:guid}")]
    public Task<ReferenceResult> Update(Guid id, UpdateReferenceRequest request, CancellationToken cancellationToken) =>
        sender.Send(new UpdateReferenceCommand(id, request.Revision, request.Title, request.SourceUrl, request.Attribution, request.Notes), cancellationToken);

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
