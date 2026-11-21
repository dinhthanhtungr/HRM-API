using HRM.Application.Features.Warehouse.Queries.GetStockAvailable;
using HRM.Application.Features.Warehouse.Queries.ExportStockAvailable;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Warehouse;

[ApiController]
[Authorize(Policy = HRM.Api.Security.Authorization.SensitiveDataAuthorization.PolicyName)]
[Route("api/v1/warehouse")]
public sealed class WarehouseStockController : ControllerBase
{
    private readonly ISender _sender;

    public WarehouseStockController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("stock-available/export")]
    public async Task<IActionResult> Export([FromQuery] GetStockAvailableQuery query, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ExportStockAvailableQuery(query), cancellationToken);
        return result.Success
            ? File(result.Data!, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"TonKho_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx")
            : BadRequest(result);
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
