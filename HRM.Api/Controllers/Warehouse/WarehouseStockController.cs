using HRM.Application.Features.Warehouse.Queries.GetStockAvailable;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Warehouse;

[ApiController]
[Authorize]
[Route("api/v1/warehouse")]
public sealed class WarehouseStockController : ControllerBase
{
    private readonly ISender _sender;

    public WarehouseStockController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("stock-available")]
    public async Task<IActionResult> GetStockAvailable(
        [FromQuery] GetStockAvailableQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);

        return result.Success
            ? Ok(result)
            : BadRequest(result);
    }
}
