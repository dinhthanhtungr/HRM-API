using HRM.Application.Features.PLM.Formulas.Queries.GetManufacturingFormulaMaterials;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.PLM.Manufacturing.Queries.ExportProductionOrderQuantity;
using HRM.Application.Features.PLM.Manufacturing.Commands.CreateLabManufacturingFormula;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulas;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulaVersions;
using HRM.Application.Features.PLM.Manufacturing.Queries.ExportManufacturingFormulaExcel;
using HRM.Application.Features.PLM.Manufacturing.Commands.PrintManufacturingOrder;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulaEditor;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulas;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulaChoices;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingFormulaSource;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[Route("api/v1/plm/manufacturing-formulas")]
public sealed class ManufacturingFormulasController : ControllerBase
{
    private readonly ISender _sender;

    public ManufacturingFormulasController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("choices")]
    public async Task<IActionResult> Choices([FromQuery] ManufacturingFormulaChoiceOptions options, CancellationToken ct)
    {
        var result = await _sender.Send(new GetManufacturingFormulaChoicesQuery(options), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{formulaId:guid}/source")]
    [Authorize(Policy = PlmPolicies.ViewFormulaMaterials)]
    public async Task<IActionResult> Source(Guid formulaId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetManufacturingFormulaSourceQuery(formulaId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet]
    [Authorize(Policy = PlmPolicies.ViewFormulaMaterials)]
    public async Task<IActionResult> List([FromQuery] ManufacturingFormulaListOptions options, CancellationToken ct)
    {
        var result = await _sender.Send(new GetManufacturingFormulasQuery(options), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("compare")]
    [Authorize(Policy = PlmPolicies.ViewFormulaMaterials)]
    public async Task<IActionResult> Compare([FromQuery] ManufacturingFormulaListOptions options, CancellationToken ct)
    {
        var result = await _sender.Send(new GetManufacturingFormulasQuery(options, Compare: true), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("quantity-excel")]
    [Authorize(Policy = PlmPolicies.ViewFormulaMaterials)]
    public async Task<IActionResult> ExportQuantity([FromQuery] string formulaExternalId, CancellationToken ct)
    {
        var result = await _sender.Send(new ExportProductionOrderQuantityQuery(formulaExternalId), ct);
        return result.Success && result.Data is not null
            ? File(result.Data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "formula-quantity.xlsx")
            : BadRequest(result);
    }

    [HttpPost("production-orders/{orderId:guid}")]
    public async Task<IActionResult> CreateForOrder(Guid orderId, [FromBody] CreateLabManufacturingFormulaRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new CreateLabManufacturingFormulaCommand(orderId, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{formulaId:guid}/versions")]
    [Authorize(Policy = PlmPolicies.ViewFormulaMaterials)]
    public async Task<IActionResult> GetVersions(Guid formulaId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetManufacturingFormulaVersionsQuery(formulaId, pageNumber, pageSize), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("production-orders/{orderId:guid}/excel")]
    [Authorize(Policy = PlmPolicies.ViewFormulaMaterials)]
    public async Task<IActionResult> ExportFormula(Guid orderId, CancellationToken ct)
    {
        var result = await _sender.Send(new ExportManufacturingFormulaExcelQuery(orderId), ct);
        return result.Success && result.Data is not null
            ? File(result.Data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"formula-{orderId:N}.xlsx")
            : BadRequest(result);
    }

    [HttpPost("production-orders/{orderId:guid}/pdf")]
    [Authorize(Policy = PlmPolicies.ViewFormulaMaterials)]
    public async Task<IActionResult> PrintOrder(Guid orderId, CancellationToken ct)
    {
        var result = await _sender.Send(new PrintManufacturingOrderCommand(orderId), ct);
        return result.Success && result.Data is not null ? File(result.Data, "application/pdf", $"production-order-{orderId:N}.pdf") : BadRequest(result);
    }

    [HttpGet("production-orders/{orderId:guid}/prefill")]
    [Authorize(Policy = PlmPolicies.ViewFormulaMaterials)]
    public async Task<IActionResult> Prefill(Guid orderId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetManufacturingFormulaEditorQuery(orderId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("production-orders/{orderId:guid}/{formulaId:guid}/view")]
    [Authorize(Policy = PlmPolicies.ViewFormulaMaterials)]
    public async Task<IActionResult> FormulaView(Guid orderId, Guid formulaId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetManufacturingFormulaEditorQuery(orderId, formulaId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{manufacturingFormulaId:guid}/materials")]
    [Authorize(Policy = PlmPolicies.ViewFormulaMaterials)]
    public async Task<IActionResult> GetMaterials(
        Guid manufacturingFormulaId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetManufacturingFormulaMaterialsQuery
        {
            ManufacturingFormulaId = manufacturingFormulaId
        }, cancellationToken);

        return result is null
            ? NotFound()
            : Ok(result);
    }
}
