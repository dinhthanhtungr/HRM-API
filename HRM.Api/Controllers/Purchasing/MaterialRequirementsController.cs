using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Purchasing.MaterialRequirements;
using HRM.Application.Features.Purchasing.MaterialRequirements.Queries.GetMaterialRequirementDemands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Purchasing;

[ApiController]
[Authorize(Policy = PurchasingPolicies.ManagePurchaseOrders)]
[Route("api/v1/purchasing/material-requirements")]
public sealed class MaterialRequirementsController(ISender sender, ICurrentUserPermissionService permissions) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] GetMaterialPurchaseRequirementsQuery query, CancellationToken ct)
    {
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("{materialId:guid}/demands")]
    public async Task<IActionResult> GetDemands(Guid materialId, [FromQuery] GetMaterialRequirementDemandsQuery query, CancellationToken ct)
    {
        if (!permissions.HasPermission(ApplicationPermissions.PLM.ViewProductionOrders) ||
            !permissions.HasPermission(ApplicationPermissions.PLM.ViewFormulaMaterials))
            return Forbid();
        query.MaterialId = materialId;
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
