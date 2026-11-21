using HRM.Application.Features.Warehouse.Queries.GetStockMovements;
using HRM.Application.Features.Warehouse.Queries.GetStockMovementByKey;
using HRM.Application.Features.Warehouse.Queries.GetStockJourney;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Warehouse;

[ApiController]
[Authorize(Policy = HRM.Api.Security.Authorization.SensitiveDataAuthorization.PolicyName)]
[Route("api/v1/warehouse/movements")]
public sealed class WarehouseMovementsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetStockMovementsQuery query, CancellationToken ct)
    {
        HRM.Api.Diagnostics.WarehouseMovementRequestTimingMiddleware.MarkDispatch(HttpContext);
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{movementKey}")]
    public async Task<IActionResult> GetDetail(string movementKey, CancellationToken ct)
    {
        var result = await sender.Send(new GetStockMovementByKeyQuery(movementKey), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpGet("{movementKey}/journey/{ledgerId:long}")]
    public async Task<IActionResult> GetJourney(string movementKey, long ledgerId, [FromQuery] int pageNumber = 1, CancellationToken ct = default)
    {
        HRM.Api.Diagnostics.WarehouseMovementRequestTimingMiddleware.MarkDispatch(HttpContext);
        var result = await sender.Send(new GetStockJourneyQuery(movementKey, ledgerId, pageNumber), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
