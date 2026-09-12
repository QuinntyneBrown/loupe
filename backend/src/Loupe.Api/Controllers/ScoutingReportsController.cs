using Loupe.Api.Scouting;
using Loupe.Application.Operations;
using Loupe.Application.Scouting;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/locations/{id:guid}/scouting-report")]
public sealed class ScoutingReportsController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OperationResult>> Create(Guid id, RequestScoutingReportRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken cancellationToken)
    {
        var operation = await sender.Send(new RequestScoutingReportCommand(id, request.Revision, request.Regenerate, key), cancellationToken);
        return Accepted($"/api/operations/{operation.Id}", operation);
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var report = await sender.Send(new GetScoutingReportQuery(id), cancellationToken);
        return report is null ? NoContent() : Ok(report);
    }

    [HttpGet("operation")]
    public async Task<ActionResult<OperationResult>> Operation(Guid id, CancellationToken cancellationToken)
    {
        var operation = await sender.Send(new GetScoutingOperationQuery(id), cancellationToken);
        return operation is null ? NoContent() : Ok(operation);
    }
}
