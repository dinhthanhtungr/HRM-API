using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Commands.CancelManufacturingVUFormula;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Commands.CreateManufacturingVUFormula;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Commands.PatchManufacturingVUFormula;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Dtos;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Queries.ExportManufacturingVUFormulaPdf;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Queries.GetManufacturingVUFormulaById;
using HRM.Application.Features.PLM.ManufacturingVUFormulas.Queries.GetManufacturingVUFormulas;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

/// <summary>
/// Quản lý lệnh sản xuất mẫu được tạo từ công thức VU.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/plm/sample-production-orders")]
public sealed class SampleProductionOrdersController : ControllerBase
{
    private readonly ISender _sender;

    public SampleProductionOrdersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [Authorize(Policy = PlmPolicies.ViewSampleProductionOrders)]
    public async Task<IActionResult> GetList(
        [FromQuery] GetManufacturingVUFormulasQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("{manufacturingVUFormulaId:guid}")]
    [Authorize(Policy = PlmPolicies.ViewSampleProductionOrders)]
    public async Task<IActionResult> GetById(
        Guid manufacturingVUFormulaId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetManufacturingVUFormulaByIdQuery(manufacturingVUFormulaId),
            cancellationToken);

        return result.Success ? Ok(result.Data) : NotFound(result);
    }

    [HttpPost]
    [Authorize(Policy = PlmPolicies.ManageSampleProductionOrders)]
    public async Task<IActionResult> Create(
        [FromBody] CreateManufacturingVUFormulaRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreateManufacturingVUFormulaCommand(request),
            cancellationToken);

        return result.Success
            ? CreatedAtAction(
                nameof(GetById),
                new { manufacturingVUFormulaId = result.Data!.ManufacturingVUFormulaId },
                result.Data)
            : BadRequest(result);
    }

    [HttpPatch("{manufacturingVUFormulaId:guid}")]
    [Authorize(Policy = PlmPolicies.ManageSampleProductionOrders)]
    public async Task<IActionResult> Patch(
        Guid manufacturingVUFormulaId,
        [FromBody] PatchManufacturingVUFormulaRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new PatchManufacturingVUFormulaCommand(manufacturingVUFormulaId, request),
            cancellationToken);

        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPost("{manufacturingVUFormulaId:guid}/cancel")]
    [Authorize(Policy = PlmPolicies.ManageSampleProductionOrders)]
    public async Task<IActionResult> Cancel(
        Guid manufacturingVUFormulaId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CancelManufacturingVUFormulaCommand(manufacturingVUFormulaId),
            cancellationToken);

        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("{manufacturingVUFormulaId:guid}/pdf")]
    [Authorize(Policy = PlmPolicies.ViewSampleProductionOrders)]
    public async Task<IActionResult> ExportPdf(
        Guid manufacturingVUFormulaId,
        [FromQuery] bool download,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ExportManufacturingVUFormulaPdfQuery(manufacturingVUFormulaId),
            cancellationToken);

        if (!result.Success || result.Data is null)
        {
            return NotFound(result);
        }

        if (download)
        {
            return File(
                result.Data.Content,
                result.Data.ContentType,
                result.Data.FileName);
        }

        Response.Headers.ContentDisposition =
            $"inline; filename*=UTF-8''{Uri.EscapeDataString(result.Data.FileName)}";
        return File(result.Data.Content, result.Data.ContentType);
    }
}
