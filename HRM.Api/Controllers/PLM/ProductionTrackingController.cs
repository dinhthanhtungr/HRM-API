using HRM.Api.Security.Authorization;
using HRM.Application.Features.PLM.Manufacturing.Queries.Tracking;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/plm/production-orders")]
public sealed class ProductionTrackingController(ISender sender) : ControllerBase
{
    [AllowImsSession]
    [HttpGet("tracking")]
    public async Task<IActionResult> List([FromQuery] string? keyword, CancellationToken ct, [FromQuery] int pageNumber = 1)
    {
        if (keyword?.Length > 100 || pageNumber < 1) return BadRequest();
        var result = await sender.Send(new ListProductionTrackingQuery(keyword, pageNumber), ct);
        return result is null ? Forbid() : Ok(result);
    }

    [AllowImsSession]
    [HttpGet("{id:guid}/tracking")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetProductionTrackingQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }
}
