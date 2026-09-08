using Loupe.Application.Comparisons;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loupe.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/comparisons")]
public sealed class ComparisonsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<ComparisonResult> Get([FromQuery] Guid firstId, [FromQuery] Guid secondId, CancellationToken cancellationToken) =>
        sender.Send(new GetComparisonQuery(firstId, secondId), cancellationToken);
}
