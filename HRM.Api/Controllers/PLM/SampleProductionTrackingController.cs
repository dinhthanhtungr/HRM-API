using HRM.Api.Security.Authorization;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Queries.Tracking;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/plm/sample-production-orders")]
public sealed class SampleProductionTrackingController(ISender sender) : ControllerBase
{
    [AllowImsSession]
    [HttpGet("tracking")]
    public async Task<IActionResult> List(CancellationToken ct, [FromQuery] int pageNumber = 1)
    {
        if (pageNumber < 1) return BadRequest();
        var result = await sender.Send(new ListSampleProductionTrackingQuery(pageNumber), ct);
        return result is null ? Forbid() : Ok(result);
    }

    [AllowImsSession]
    [HttpGet("{id:guid}/tracking")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetSampleProductionTrackingQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }
}
