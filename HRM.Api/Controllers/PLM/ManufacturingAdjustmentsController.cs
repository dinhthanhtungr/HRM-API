using HRM.Application.Features.PLM.Manufacturing.Queries.InitializeManufacturingAdjustment;
using HRM.Application.Features.PLM.Manufacturing.Queries.CalculateManufacturingAdjustment;
using HRM.Application.Features.PLM.Manufacturing.Commands.DeleteManufacturingAdjustment;
using HRM.Application.Features.PLM.Manufacturing.Commands.SaveManufacturingAdjustment;
using HRM.Application.Features.PLM.Manufacturing.Commands.SaveManufacturingAdjustmentRange;
using HRM.Application.Features.PLM.Manufacturing.Queries.ExportManufacturingAdjustmentPdf;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingAdjustment;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingAdjustmentGroups;
using HRM.Application.Features.PLM.Manufacturing.Queries.ListManufacturingAdjustments;
using HRM.Application.Features.PLM.Manufacturing.Queries.PreviewManufacturingAdjustmentPdf;

using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[Authorize]
[ApiController]
[Route("api/v1/plm/manufacturing-adjustments")]
public sealed class ManufacturingAdjustmentsController(ISender sender) : ControllerBase
{
    [HttpGet("production-orders/{orderId:guid}/initialize")]
    public async Task<IActionResult> Initialize(Guid orderId, Guid? adjustmentId, CancellationToken ct)
    {
        var result = await sender.Send(new InitializeManufacturingAdjustmentQuery(orderId, adjustmentId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("production-orders/{orderId:guid}/calculate")]
    public async Task<IActionResult> Calculate(Guid orderId, Guid? adjustmentId, [FromBody] ManufacturingAdjustmentInput request, CancellationToken ct)
    {
        var result = await sender.Send(new CalculateManufacturingAdjustmentQuery(orderId, request, adjustmentId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] ListManufacturingAdjustmentsQuery query, CancellationToken ct)
    {
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, int? batchNo, CancellationToken ct)
    {
        var result = await sender.Send(new ExportManufacturingAdjustmentPdfQuery(id, batchNo), ct);
        return result.Success && result.Data is not null ? File(result.Data, "application/pdf", $"PhieuChinhMau_{id:N}.pdf") : BadRequest(result);
    }
    [HttpPost("production-orders/{orderId:guid}/pdf-preview")]
    public async Task<IActionResult> Preview(Guid orderId, Guid? adjustmentId, [FromBody] ManufacturingAdjustmentInput request, CancellationToken ct)
    {
        var result = await sender.Send(new PreviewManufacturingAdjustmentPdfQuery(orderId, request, adjustmentId), ct);
        return result.Success && result.Data is not null ? File(result.Data, "application/pdf", "PhieuChinhMau_Preview.pdf") : BadRequest(result);
    }
    [HttpGet("production-orders/{orderId:guid}")]
    public async Task<IActionResult> List(Guid orderId, [FromQuery] ListManufacturingAdjustmentsQuery query, CancellationToken ct)
    {
        var result = await sender.Send(query with { OrderId = orderId }, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetManufacturingAdjustmentQuery(id), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
    [HttpGet("{id:guid}/batch-groups")]
    public async Task<IActionResult> Groups(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetManufacturingAdjustmentGroupsQuery(id), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
    [HttpGet("production-orders/{orderId:guid}/batch-groups")]
    public async Task<IActionResult> GroupsByOrder(Guid orderId, CancellationToken ct)
    {
        var result = await sender.Send(new GetManufacturingAdjustmentGroupsQuery(orderId, true), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
    [HttpPost("production-orders/{orderId:guid}")]
    public async Task<IActionResult> Create(Guid orderId, [FromBody] ManufacturingAdjustmentInput request, CancellationToken ct)
    {
        var result = await sender.Send(new SaveManufacturingAdjustmentCommand(orderId, null, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
    [HttpPatch("production-orders/{orderId:guid}/{id:guid}")]
    public async Task<IActionResult> Patch(Guid orderId, Guid id, [FromBody] ManufacturingAdjustmentInput request, CancellationToken ct)
    {
        var result = await sender.Send(new SaveManufacturingAdjustmentCommand(orderId, id, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
    [HttpPost("{id:guid}/batches")]
    public async Task<IActionResult> AddBatch(Guid id, [FromBody] ManufacturingAdjustmentRangeInput request, CancellationToken ct)
    {
        var result = await sender.Send(new SaveManufacturingAdjustmentRangeCommand(id, null, request, true), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
    [HttpPost("{id:guid}/batch-ranges")]
    public async Task<IActionResult> AddRange(Guid id, [FromBody] ManufacturingAdjustmentRangeInput request, CancellationToken ct)
    {
        var result = await sender.Send(new SaveManufacturingAdjustmentRangeCommand(id, null, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
    [HttpPatch("{id:guid}/batches/{batchId:guid}")]
    public async Task<IActionResult> PatchRange(Guid id, Guid batchId, [FromBody] ManufacturingAdjustmentRangeInput request, CancellationToken ct)
    {
        var result = await sender.Send(new SaveManufacturingAdjustmentRangeCommand(id, batchId, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
    [HttpDelete("{id:guid}/batches/{batchId:guid}")]
    public async Task<IActionResult> DeleteBatch(Guid id, Guid batchId, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteManufacturingAdjustmentCommand(id, batchId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DeleteManufacturingAdjustmentCommand(id), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
