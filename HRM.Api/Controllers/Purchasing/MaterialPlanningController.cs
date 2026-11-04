using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Purchasing.MaterialPlanning.Queries.GetPlanningOrders;
using HRM.Application.Features.Purchasing.MaterialPlanning.Queries.GetPlanningLines;
using HRM.Application.Features.Purchasing.MaterialPlanning.Queries.ExpandPlanningNode;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Purchasing;

[ApiController]
[Authorize(Policy = PurchasingPolicies.ManagePurchaseOrders)]
[Route("api/v1/purchasing/material-planning")]
public sealed class MaterialPlanningController(ISender sender, ICurrentUserPermissionService permissions) : ControllerBase
{
    [HttpGet("orders")]
    public async Task<IActionResult> Orders([FromQuery] GetPlanningOrdersQuery query, CancellationToken ct)
    {
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("orders/{orderId:guid}/lines")]
    public async Task<IActionResult> Lines(Guid orderId, [FromQuery] GetPlanningLinesQuery query, CancellationToken ct)
    {
        if (!permissions.HasPermission(ApplicationPermissions.PLM.ViewProductionOrders)) return Forbid();
        query.OrderId = orderId;
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPost("orders/{orderId:guid}/lines/{lineId:guid}/expansion")]
    public async Task<IActionResult> Expand(Guid orderId, Guid lineId, [FromBody] ExpandPlanningNodeQuery query, CancellationToken ct)
    {
        if (!permissions.HasPermission(ApplicationPermissions.PLM.ViewProductionOrders) ||
            !permissions.HasPermission(ApplicationPermissions.PLM.ViewFormulaMaterials)) return Forbid();
        query.OrderId = orderId;
        query.LineId = lineId;
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
