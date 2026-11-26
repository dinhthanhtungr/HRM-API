using HRM.Application.Features.ExcelSpecial.Queries.ExportMaterialUsage;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.ExcelSpecial.Queries.ExportMerchandiseOrderVa;
using HRM.Application.Features.ExcelSpecial.Queries.ExportMaterials;
using HRM.Application.Features.ExcelSpecial.Queries.ExportManufacturingVa;
using HRM.Application.Features.Dispatch.DeliveryOrders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.ExcelSpecial;

[ApiController]
[Authorize]
[Tags("Excel Special")]
[Route("api/v1/excel-special")]
public sealed class ExcelSpecialController(ISender sender) : ControllerBase
{
    [HttpPost("materials/va-usage/excel")]
    [Authorize(Roles = ApplicationRoles.Developer)]
    public async Task<IActionResult> ExportMaterialUsage([FromBody] ExportMaterialUsageQuery query, CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(query, ct);
            return result.Success && result.Data is not null
                ? File(result.Data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "material-va-usage.xlsx")
                : BadRequest(result);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
    [HttpGet("manufacturing/va/excel")]
    public async Task<IActionResult> ExportManufacturingVa(
        [FromQuery] ExportManufacturingVaQuery query, CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(query, ct);
            return result.Success && result.Data is not null
                ? File(result.Data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "manufacturing-va.xlsx")
                : BadRequest(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("merchandise-orders/va/excel")]
    [Authorize(Roles = DeliveryOrderAccessRules.ReadRoles)]
    public async Task<IActionResult> ExportMerchandiseOrderVa(
        [FromQuery] ExportMerchandiseOrderVaQuery query, CancellationToken ct)
    {
        var result = await sender.Send(query, ct);
        return result.Success && result.Data is not null
            ? File(result.Data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "merchandise-orders-va.xlsx")
            : BadRequest(result);
    }

    [HttpGet("materials/excel")]
    public async Task<IActionResult> ExportMaterials(CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(new ExportMaterialsQuery(), ct);
            return result.Success && result.Data is not null
                ? File(result.Data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "materials.xlsx")
                : BadRequest(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
