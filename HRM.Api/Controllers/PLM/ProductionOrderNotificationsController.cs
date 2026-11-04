using HRM.Application.Features.PLM.Manufacturing.Commands.NotifyPromisedDelivery;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[Route("api/v1/plm/production-orders")]
public sealed class ProductionOrderNotificationsController(ISender sender) : ControllerBase
{
    [HttpPost("notify-promised-delivery")]
    public async Task<IActionResult> NotifyPromisedDelivery([FromBody] NotifyPromisedDeliveryCommand request, CancellationToken ct)
    {
        var result = await sender.Send(request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
