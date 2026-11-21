using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Reports.DeliveryOtd.Queries.GetDeliveryOtdHeader;
using HRM.Application.Features.Reports.DeliveryOtd.Queries.GetDeliveryOtdCharts;
using HRM.Application.Features.Reports.DeliveryOtd.Queries.GetDeliveryOtdDetails;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Reports;

[ApiController]
[Authorize(Policy = ApplicationPermissions.Reports.ViewDeliveryOtd)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/reports/delivery-otd")]
public sealed class DeliveryOtdReportController(ISender sender) : ControllerBase
{
    [HRM.Api.Security.Authorization.AllowImsSession]
    [HttpGet("header")]
    public async Task<IActionResult> GetHeader([FromQuery] GetDeliveryOtdHeaderQuery query, CancellationToken ct)
    {
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HRM.Api.Security.Authorization.AllowImsSession]
    [HttpGet("charts")]
    public async Task<IActionResult> GetCharts([FromQuery] GetDeliveryOtdChartsQuery query, CancellationToken ct)
    {
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HRM.Api.Security.Authorization.AllowImsSession]
    [HttpGet("details")]
    public async Task<IActionResult> GetDetails([FromQuery] GetDeliveryOtdDetailsQuery query, CancellationToken ct)
    {
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
