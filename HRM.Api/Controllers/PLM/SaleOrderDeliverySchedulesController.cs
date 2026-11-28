using HRM.Application.Features.PLM.SaleOrders.DeliverySchedules;
using HRM.Application.Commons.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController, Authorize(Policy = HRM.Api.Security.Authorization.SensitiveDataAuthorization.PolicyName)]
[Route("api/v1/plm/sale-orders/{orderId:guid}/details/{detailId:guid}/delivery-schedules")]
public sealed class SaleOrderDeliverySchedulesController(DeliveryScheduleService service) : ControllerBase
{
    [HttpGet("~/api/v1/plm/delivery-schedules/features")]
    public IActionResult Features() => Ok(OperationResult<DeliveryScheduleFeatureDto>.Ok(service.Features()));
    [HttpGet]
    public async Task<IActionResult> Get(Guid orderId, Guid detailId, CancellationToken ct)
    {
        var data = await service.GetAsync(orderId, detailId, ct);
        return data is null ? NotFound() : Ok(OperationResult<DeliveryScheduleWorkspaceDto>.Ok(data));
    }
    [HttpPut]
    public async Task<IActionResult> Save(Guid orderId, Guid detailId, SaveDeliverySchedulesRequest request, CancellationToken ct)
    {
        var result = await service.SaveAsync(orderId, detailId, request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
    [HttpPost("allocation-preview")]
    public async Task<IActionResult> Preview(Guid orderId, Guid detailId, DeliverySchedulePreviewRequest request, CancellationToken ct)
    {
        var result = await service.PreviewAsync(orderId, detailId, request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPatch("promises")]
    public async Task<IActionResult> UpdatePromises(Guid orderId, Guid detailId, UpdateDeliveryPromisesRequest request, CancellationToken ct)
    {
        var result = await service.UpdatePromisesAsync(orderId, detailId, request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
