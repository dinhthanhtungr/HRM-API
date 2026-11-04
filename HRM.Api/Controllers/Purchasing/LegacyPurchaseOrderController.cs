using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Purchasing.PurchaseOrders.Commands.CreatePurchaseOrder;
using HRM.Application.Features.Purchasing.PurchaseOrders.Commands.UpdatePurchaseOrderNotes;
using HRM.Application.Features.Purchasing.PurchaseOrders.Commands.CompletePurchaseOrder;
using HRM.Application.Features.Purchasing.PurchaseOrders.Dtos;
using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrders;
using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderById;
using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.ExportPurchaseOrderPdf;
using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.ExportPurchaseOrdersExcel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HRM.Api.Controllers.Purchasing;
/// <summary>Giữ route/request/response wrapper của API mua hàng cũ; dùng cùng handler có scope của HRM.</summary>
[ApiController]
[Authorize(Policy = PurchasingPolicies.ManagePurchaseOrders)]
[Route("api/PurchaseOrder")]
public sealed class LegacyPurchaseOrderController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] GetPurchaseOrdersQuery query, CancellationToken ct) =>
        Ok(OperationResult<HRM.Application.Commons.Pagination.PagedResult<PurchaseOrderListItemDto>>.Ok(await sender.Send(query, ct)));

    [HttpGet("{purchaseOrderId:guid}")]
    public async Task<IActionResult> Detail(Guid purchaseOrderId, CancellationToken ct)
    {
        var result = await sender.Send(new GetPurchaseOrderByIdQuery(purchaseOrderId), ct);
        return result is null ? NotFound() : Ok(OperationResult<PurchaseOrderDetailDto>.Ok(result));
    }
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] LegacyCreatePurchaseOrderRequest input, CancellationToken ct)
    {
        var result = await sender.Send(new CreatePurchaseOrderCommand(input.ToRequest()), ct);
        return result.Success && result.Data is not null
            ? Ok(OperationResult<Guid>.Ok(result.Data.PurchaseOrderId, result.Message)) : BadRequest(result);
    }
    [HttpPatch]
    public async Task<IActionResult> Update([FromBody] LegacyUpdatePurchaseOrderRequest input, CancellationToken ct)
    {
        var result = await sender.Send(new UpdatePurchaseOrderNotesCommand(input.PurchaseOrderId, new()
        {
            Comment = input.Comment,
            PlpuComment = input.PLPUComment,
            IsActive = input.IsActive,
            ClearFields = input.ClearFields
        }), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
    [HttpPatch("{purchaseOrderId:guid}/complete")]
    public async Task<IActionResult> Complete(Guid purchaseOrderId, CancellationToken ct)
    {
        var result = await sender.Send(new CompletePurchaseOrderCommand(purchaseOrderId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
    [HttpGet("GeneratePdf")]
    public async Task<IActionResult> Pdf([FromQuery] Guid purchaseOrderId, CancellationToken ct)
    {
        var result = await sender.Send(new ExportPurchaseOrderPdfQuery(purchaseOrderId), ct);
        return result.Success && result.Data is not null ? File(result.Data.Content, result.Data.ContentType, "PurchaseOrder.pdf") : BadRequest(result);
    }
    [HttpGet("ExportExcel")]
    public async Task<IActionResult> Excel([FromQuery] GetPurchaseOrdersQuery query, CancellationToken ct)
    {
        var result = await sender.Send(new ExportPurchaseOrdersExcelQuery(query), ct);
        return File(result.Content, result.ContentType, result.FileName);
    }
}
