using Loupe.Api.References;
using Loupe.Application.Operations;
using Loupe.Application.ReferenceAnalysis;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/references/{id:guid}/analysis")]
public sealed class ReferenceAnalysisController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OperationResult>> Create(Guid id, RequestReferenceAnalysisRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken cancellationToken)
    {
        var operation = await sender.Send(new RequestReferenceAnalysisCommand(id, request.Revision, key), cancellationToken);
        return Accepted($"/api/operations/{operation.Id}", operation);
    }
    [HttpGet]
    public async Task<ActionResult<OperationResult>> Get(Guid id, CancellationToken cancellationToken)
    {
        var operation = await sender.Send(new GetReferenceAnalysisQuery(id), cancellationToken);
        return operation is null ? NoContent() : Ok(operation);
    }
}
