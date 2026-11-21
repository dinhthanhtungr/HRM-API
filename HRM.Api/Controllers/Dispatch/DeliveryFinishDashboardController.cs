using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetFinishDashboard;
using HRM.Application.Commons.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Dispatch;

[ApiController]
[Authorize(Policy = ApplicationPermissions.Reports.ViewDeliveryFinish)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/dispatch/reports/finish-dashboard")]
public sealed class DeliveryFinishDashboardController(ISender sender) : ControllerBase
{
    [HRM.Api.Security.Authorization.AllowImsSession]
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] GetFinishDashboardQuery query, CancellationToken ct)
    {
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
