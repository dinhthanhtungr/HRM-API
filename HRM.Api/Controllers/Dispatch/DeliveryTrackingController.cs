using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.Tracking;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Dispatch;

[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/dispatch/delivery-orders")]
public sealed class DeliveryTrackingController(ISender sender) : ControllerBase
{
    [HRM.Api.Security.Authorization.AllowImsSession]
    [HttpGet("tracking")]
    public async Task<IActionResult> List(CancellationToken ct, [FromQuery] int pageNumber = 1)
    {
        if (pageNumber < 1) return BadRequest();
        var result = await sender.Send(new ListDeliveryTrackingQuery(pageNumber), ct);
        return result is null ? Forbid() : Ok(result);
    }

    [HRM.Api.Security.Authorization.AllowImsSession]
    [HttpGet("{id:guid}/tracking")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetDeliveryTrackingQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }
}
