using Loupe.Application.Operations;
using Loupe.Application.Critiques;
using Loupe.Api.Operations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/operations")]
public sealed class OperationsController(ISender sender) : ControllerBase
{
    [HttpPost("{id:guid}/retry")]
    public async Task<ActionResult<OperationResult>> Retry(Guid id, RetryCritiqueRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? operationKey, CancellationToken cancellationToken)
    {
        var operation = await sender.Send(new RetryCritiqueCommand(id, request.Revision, operationKey), cancellationToken);
        return Accepted($"/api/operations/{operation.Id}", operation);
    }

    [HttpGet("{id:guid}")]
    public Task<OperationResult> Get(Guid id, CancellationToken cancellationToken) => sender.Send(new GetOperationQuery(id), cancellationToken);
}
