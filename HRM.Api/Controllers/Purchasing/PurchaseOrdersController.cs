using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Purchasing.PurchaseOrders.Commands.CancelPurchaseOrder;
using HRM.Application.Features.Purchasing.PurchaseOrders.Commands.CompletePurchaseOrder;
using HRM.Application.Features.Purchasing.PurchaseOrders.Commands.CreatePurchaseOrder;
using HRM.Application.Features.Purchasing.PurchaseOrders.Commands.DeletePurchaseOrder;
using HRM.Application.Features.Purchasing.PurchaseOrders.Commands.SubmitPurchaseOrder;
using HRM.Application.Features.Purchasing.PurchaseOrders.Commands.UpdatePurchaseOrderNotes;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderById;
using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrders;
using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.ExportPurchaseOrderExcel;
using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.ExportPurchaseOrderPdf;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Purchasing;

[ApiController]
[Authorize(Policy = PurchasingPolicies.ManagePurchaseOrders)]
[Route("api/v1/purchasing/purchase-orders")]
public sealed class PurchaseOrdersController : ControllerBase
{
    private readonly ISender _sender;
    public PurchaseOrdersController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetPurchaseOrdersQuery query, CancellationToken ct) => Ok(await _sender.Send(query, ct));

    [HttpGet("{purchaseOrderId:guid}")]
    public async Task<IActionResult> GetById(Guid purchaseOrderId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetPurchaseOrderByIdQuery(purchaseOrderId), ct);
        return result is null ? NotFound(new { message = "Không tìm thấy PO." }) : Ok(result);
    }

    [HttpGet("{purchaseOrderId:guid}/pdf")]
    public async Task<IActionResult> ExportPdf(Guid purchaseOrderId, CancellationToken ct)
    {
        var result = await _sender.Send(new ExportPurchaseOrderPdfQuery(purchaseOrderId), ct);
        return result.Success && result.Data is not null
            ? File(result.Data.Content, result.Data.ContentType, result.Data.FileName)
            : BadRequest(result);
    }

    [HttpGet("{purchaseOrderId:guid}/excel")]
    public async Task<IActionResult> ExportExcel(Guid purchaseOrderId, CancellationToken ct)
    {
        var result = await _sender.Send(new ExportPurchaseOrderExcelQuery(purchaseOrderId), ct);
        return result.Success && result.Data is not null
            ? File(result.Data.Content, result.Data.ContentType, result.Data.FileName)
            : BadRequest(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseOrderRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new CreatePurchaseOrderCommand(request), ct);
        return result.Success ? StatusCode(StatusCodes.Status201Created, result) : BadRequest(result);
    }

    [HttpPatch("{purchaseOrderId:guid}")]
    public async Task<IActionResult> UpdateNotes(
        Guid purchaseOrderId,
        [FromBody] UpdatePurchaseOrderNotesRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(new UpdatePurchaseOrderNotesCommand(purchaseOrderId, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{purchaseOrderId:guid}")]
    public async Task<IActionResult> Delete(Guid purchaseOrderId, CancellationToken ct)
    {
        var result = await _sender.Send(new DeletePurchaseOrderCommand(purchaseOrderId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{purchaseOrderId:guid}/submit")]
    public async Task<IActionResult> Submit(Guid purchaseOrderId, CancellationToken ct)
    {
        var result = await _sender.Send(new SubmitPurchaseOrderCommand(purchaseOrderId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{purchaseOrderId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid purchaseOrderId, [FromBody] CancelPurchaseOrderRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new CancelPurchaseOrderCommand(purchaseOrderId, request.Reason), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{purchaseOrderId:guid}/complete")]
    public async Task<IActionResult> Complete(Guid purchaseOrderId, CancellationToken ct)
    {
        var result = await _sender.Send(new CompletePurchaseOrderCommand(purchaseOrderId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
