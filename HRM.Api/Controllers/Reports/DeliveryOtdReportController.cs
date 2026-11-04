using HRM.Application.Features.Dispatch.DeliveryOrders;
using HRM.Application.Features.Reports.DeliveryOtd.Queries.GetDeliveryOtdHeader;
using HRM.Application.Features.Reports.DeliveryOtd.Queries.GetDeliveryOtdCharts;
using HRM.Application.Features.Reports.DeliveryOtd.Queries.GetDeliveryOtdDetails;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Reports;

[ApiController]
[Authorize(Roles = DeliveryOrderAccessRules.ReadRoles)]
[Route("api/v1/reports/delivery-otd")]
public sealed class DeliveryOtdReportController(ISender sender) : ControllerBase
{
    [HttpGet("header")]
    public async Task<IActionResult> GetHeader([FromQuery] GetDeliveryOtdHeaderQuery query, CancellationToken ct)
    {
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("charts")]
    public async Task<IActionResult> GetCharts([FromQuery] GetDeliveryOtdChartsQuery query, CancellationToken ct)
    {
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("details")]
    public async Task<IActionResult> GetDetails([FromQuery] GetDeliveryOtdDetailsQuery query, CancellationToken ct)
    {
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
