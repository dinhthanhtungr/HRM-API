using HRM.Application.Features.PLM.SaleOrders.Queries.Tracking;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/plm/sale-orders")]
public sealed class SaleOrderTrackingController(ISender sender) : ControllerBase
{
    [HRM.Api.Security.Authorization.AllowImsSession]
    [HttpGet("tracking")]
    public async Task<IActionResult> List(CancellationToken cancellationToken, [FromQuery] int pageNumber = 1)
    {
        if (pageNumber < 1) return BadRequest();
        var result = await sender.Send(new ListSaleOrderTrackingQuery(pageNumber), cancellationToken);
        return result is null ? Forbid() : Ok(result);
    }

    [HRM.Api.Security.Authorization.AllowImsSession]
    [HttpGet("{orderId:guid}/tracking")]
    public async Task<IActionResult> Get(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSaleOrderTrackingQuery(orderId), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
