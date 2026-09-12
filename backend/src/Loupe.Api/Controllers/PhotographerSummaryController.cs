using Loupe.Api.Photographers;
using Loupe.Application.Operations;
using Loupe.Application.PhotographerSummaries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/photographers/{id:guid}/summary-analysis")]
public sealed class PhotographerSummaryController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OperationResult>> Create(Guid id, RequestPhotographerSummaryRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken cancellationToken)
    {
        var operation = await sender.Send(new RequestPhotographerSummaryCommand(id, request.Revision, key), cancellationToken);
        return Accepted($"/api/operations/{operation.Id}", operation);
    }
    [HttpGet]
    public async Task<ActionResult<OperationResult>> Get(Guid id, CancellationToken cancellationToken)
    {
        var operation = await sender.Send(new GetPhotographerSummaryQuery(id), cancellationToken);
        return operation is null ? NoContent() : Ok(operation);
    }
}
