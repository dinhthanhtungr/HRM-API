using HRM.Application.Features.ExcelSpecial.Queries.ExportMerchandiseOrderVa;
using HRM.Application.Features.Dispatch.DeliveryOrders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.ExcelSpecial;

[ApiController]
[Authorize(Roles = DeliveryOrderAccessRules.ReadRoles)]
[Tags("Excel Special")]
[Route("api/v1/excel-special")]
public sealed class ExcelSpecialController(ISender sender) : ControllerBase
{
    [HttpGet("merchandise-orders/va/excel")]
    public async Task<IActionResult> ExportMerchandiseOrderVa(
        [FromQuery] ExportMerchandiseOrderVaQuery query, CancellationToken ct)
    {
        var result = await sender.Send(query, ct);
        return result.Success && result.Data is not null
            ? File(result.Data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "merchandise-orders-va.xlsx")
            : BadRequest(result);
    }
}
