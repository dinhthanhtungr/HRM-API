using HRM.Application.Features.Dispatch.Deliverers.Queries.GetDeliverers;
using HRM.Application.Features.Dispatch.DeliveryOrders.Commands.CreateDeliveryOrder;
using HRM.Application.Features.Dispatch.DeliveryOrders.Commands.UpdateDeliveryOrder;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryOrderDetail;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryOrders;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetSelectableDeliveryLines;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Domain.Entities.Controllers.Dispatch;

[ApiController]
[Route("api/v1/dispatch")]
public sealed class DeliveryOrdersController : ControllerBase
{
    private readonly ISender _sender;

    public DeliveryOrdersController(ISender sender)
    {
        _sender = sender;
    }

    // =================================================== Queries ===================================================

    [HttpGet("delivery-orders")]
    public async Task<IActionResult> GetDeliveryOrders(
        [FromQuery] GetDeliveryOrdersQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("delivery-orders/{id:guid}")]
    public async Task<IActionResult> GetDeliveryOrderDetail(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetDeliveryOrderDetailQuery(id), cancellationToken);

        if (result is null)
        {
            return NotFound(new { message = "Delivery order not found." });
        }

        return Ok(result);
    }

    [HttpGet("delivery-orders/selectable-lines")]
    public async Task<IActionResult> GetSelectableDeliveryLines(
        [FromQuery] GetSelectableDeliveryLinesQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("deliverers")]
    public async Task<IActionResult> GetDeliverers(
        [FromQuery] GetDeliverersQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);

        return Ok(result);
    }

    // =================================================== Commands ===================================================

    [HttpPost("delivery-orders")]
    public async Task<IActionResult> CreateDeliveryOrder(
    [FromBody] CreateDeliveryOrderCommand command,
    CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPut("delivery-orders/{id:guid}")]
    public async Task<IActionResult> UpdateDeliveryOrder(
        [FromRoute] Guid id,
        [FromBody] UpdateDeliveryOrderCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id)
            return BadRequest(new { message = "Id trên URL và body không khớp." });

        var result = await _sender.Send(command, cancellationToken);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

}
