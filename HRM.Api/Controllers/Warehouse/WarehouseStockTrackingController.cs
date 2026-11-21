using HRM.Application.Features.Warehouse.Queries.GetWarehouseStockTracking;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Warehouse;

[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/warehouse/stock-available/tracking")]
public sealed class WarehouseStockTrackingController(ISender sender) : ControllerBase
{
    [HRM.Api.Security.Authorization.AllowImsSession]
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? keyword,
        CancellationToken cancellationToken,
        [FromQuery] int pageNumber = 1)
    {
        if (keyword?.Length > 100 || pageNumber < 1) return BadRequest();
        var result = await sender.Send(new GetWarehouseStockTrackingQuery(keyword, pageNumber), cancellationToken);
        return result is null ? Forbid() : Ok(result);
    }
}
