using Loupe.Api.Critiques;
using Loupe.Application.Critiques;
using Loupe.Application.Operations;
using Loupe.Domain.Critiques;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/photographs/{id:guid}/critique")]
public sealed class CritiquesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SavedCritique>> Get(Guid id, CancellationToken cancellationToken)
    {
        var critique = await sender.Send(new GetCritiqueQuery(id), cancellationToken);
        return critique is null ? NoContent() : Ok(critique);
    }

    [HttpPost]
    public async Task<ActionResult<OperationResult>> Create(Guid id, RequestCritiqueRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? operationKey, CancellationToken cancellationToken)
    {
        var operation = await sender.Send(new RequestCritiqueCommand(id, request.Revision, request.Regenerate, operationKey), cancellationToken);
        return Accepted($"/api/operations/{operation.Id}", operation);
    }
}
