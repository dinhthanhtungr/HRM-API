using HRM.Application.Features.Dispatch.Deliverers.Queries.GetDeliverers;
using HRM.Application.Features.Dispatch.DeliveryOrders.Commands.BackfillLotConsumptions;
using HRM.Application.Features.Dispatch.DeliveryOrders.Commands.CancelDeliveryOrder;
using HRM.Application.Features.Dispatch.DeliveryOrders.Commands.ChangeStatus;
using HRM.Application.Features.Dispatch.DeliveryOrders.Commands.CreateDeliveryOrder;
using HRM.Application.Features.Dispatch.DeliveryOrders.Commands.UpdateDeliveryOrder;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos;
using HRM.Application.Features.Dispatch.DeliveryOrders;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryOrderDetail;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryOrders;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetSelectableDeliveryLines;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetAvailableLots;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HRM.Domain.Security.Rules.Roles;

namespace HRM.Domain.Entities.Controllers.Dispatch;

[ApiController]
[Authorize(Roles = DeliveryOrderAccessRules.ReadRoles)]
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

    [HttpGet("delivery-orders/available-lots")]
    public async Task<IActionResult> GetAvailableLots(
        [FromQuery] GetAvailableDeliveryLotsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
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

    [HttpPost("delivery-orders/lot-consumptions/backfill")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> BackfillLotConsumptions(
        [FromQuery] bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new BackfillDeliveryLotConsumptionsCommand(dryRun),
            cancellationToken);

        return result.Success
            ? Ok(result)
            : BadRequest(result);
    }

    [HttpPost("delivery-orders")]
    [Authorize(Roles = DeliveryOrderAccessRules.ManageRoles)]
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
    [Authorize(Roles = DeliveryOrderAccessRules.ManageRoles)]
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

    [HttpPut("delivery-orders/{id:guid}/status")]
    [Authorize(Roles = DeliveryOrderAccessRules.ManageRoles)]
    public async Task<IActionResult> ChangeDeliveryOrderStatus(
        [FromRoute] Guid id,
        [FromBody] ChangeDeliveryOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ChangeDeliveryOrderStatusCommand(id, request.Status),
            cancellationToken);

        return result.Success
            ? Ok(result)
            : BadRequest(result);
    }

    [HttpPost("delivery-orders/{id:guid}/cancel")]
    [Authorize(Roles = DeliveryOrderAccessRules.CancelRoles)]
    public async Task<IActionResult> CancelDeliveryOrder(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CancelDeliveryOrderCommand(id),
            cancellationToken);

        return result.Success
            ? Ok(result)
            : BadRequest(result);
    }

}
