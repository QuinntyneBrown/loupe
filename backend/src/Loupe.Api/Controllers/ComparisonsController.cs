using Loupe.Application.Comparisons;
using Loupe.Application.Photographs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/comparisons")]
public sealed class ComparisonsController(ISender sender) : ControllerBase
{
    [HttpGet("eligible")]
    public Task<PhotographPage> Eligible([FromQuery] int pageSize = 24, [FromQuery] string? cursor = null, CancellationToken cancellationToken = default) =>
        sender.Send(new ListComparisonCandidatesQuery(pageSize, cursor), cancellationToken);

    [HttpGet]
    public Task<ComparisonResult> Get([FromQuery] Guid firstId, [FromQuery] Guid secondId, CancellationToken cancellationToken) =>
        sender.Send(new GetComparisonQuery(firstId, secondId), cancellationToken);
}
