using Loupe.Api.Photographers;
using Loupe.Application.PhotographerDrafts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/photographer-drafts")]
public sealed class PhotographerDraftsController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PhotographerDraftResult>> Import(ImportPhotographerDraftRequest request, [FromHeader(Name = "Idempotency-Key")] string? operationKey, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ImportPhotographerDraftCommand(request.PortfolioUrl, request.Name, operationKey), cancellationToken);
        return Created($"/api/photographer-drafts/{result.Id}", result);
    }
    [HttpGet("{id:guid}")]
    public Task<PhotographerDraftResult> Get(Guid id, CancellationToken cancellationToken) => sender.Send(new GetPhotographerDraftQuery(id), cancellationToken);
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new CancelPhotographerDraftCommand(id), cancellationToken); return NoContent();
    }
}
