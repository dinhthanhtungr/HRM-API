using HRM.Application.Features.PLM.ProductionOrders.Dtos;
using HRM.Application.Features.PLM.ProductionOrders.Commands.CreateInternalProductionOrder;
using HRM.Application.Features.PLM.ProductionOrders.Commands.CreateProductionOrderInform;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ProductionOrders.Queries.CheckProductInProduction;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrder;
using HRM.Application.Features.PLM.Manufacturing.Commands.CheckProductionOrder;
using HRM.Application.Features.PLM.Manufacturing.Commands.FinishProductionOrder;
using HRM.Application.Features.PLM.Manufacturing.Commands.UpdateProductionOrderInformation;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;
using HRM.Application.Features.PLM.Manufacturing.Commands.UseProductionOrderStock;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrderWorkspace;
using HRM.Application.Features.PLM.Manufacturing.Commands.CreateProductionOrderFormula;
using HRM.Application.Features.PLM.Manufacturing.Commands.UpdateProductionOrderInform;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrderFormulaSource;
using HRM.Application.Features.PLM.Manufacturing.Commands.ProcessManufacturingFormula;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulas;
using HRM.Application.Features.PLM.Manufacturing.Commands.CopyProductionOrder;
using HRM.Application.Features.PLM.Manufacturing.Commands.CreateBasicInternalProductionOrder;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetProductionOrderInform;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingOperationHistory;
using HRM.Application.Features.PLM.Manufacturing.Commands.ApplyProductionOrderAction;
using HRM.Application.Features.PLM.Manufacturing.Commands.ExecuteProductionOrderEditorAction;
using HRM.Application.Features.PLM.Manufacturing.Queries.GetManufacturingSteps;
using HRM.Application.Features.PLM.Manufacturing.Commands.CreateBasicInformProductionOrder;

namespace HRM.Api.Controllers.PLM;

/// <summary>
/// Cung cấp thao tác đọc và tạo lệnh sản xuất MFG.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/plm/production-orders")]
public sealed class ProductionOrdersController : ControllerBase
{
    private readonly ISender _sender;

    public ProductionOrdersController(ISender sender) => _sender = sender;

    [HttpGet("step-options")]
    public async Task<IActionResult> StepOptions(CancellationToken ct)
    {
        var result = await _sender.Send(new GetManufacturingStepsQuery(), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/actions")]
    public async Task<IActionResult> ExecuteEditorAction(Guid id,
        [FromBody] ProductionOrderEditorRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(new ExecuteProductionOrderEditorActionCommand(id, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/actions/{action}")]
    public async Task<IActionResult> ApplyAction(Guid id, ManufacturingInformationAction action,
        [FromBody] ManufacturingInformationActionRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new ApplyProductionOrderActionCommand(id, action, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id:guid}/formula-sources/{sourceId:guid}/operation-history")]
    public async Task<IActionResult> OperationHistory(Guid id, Guid sourceId, [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 100, CancellationToken ct = default)
    {
        var result = await _sender.Send(new GetManufacturingOperationHistoryQuery(id, sourceId, pageNumber, pageSize), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("internal-basic")]
    public async Task<IActionResult> CreateInternalBasic([FromBody] CreateBasicInternalProductionOrderRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new CreateBasicInternalProductionOrderCommand(request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("inform-basic")]
    public async Task<IActionResult> CreateInformBasic([FromBody] CreateBasicInformProductionOrderRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new CreateBasicInformProductionOrderCommand(request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/copy")]
    public async Task<IActionResult> Copy(Guid id, [FromBody] ProductionOrderInformRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new CopyProductionOrderCommand(id, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/formulas/{formulaId:guid}/process")]
    public async Task<IActionResult> ProcessFormula(Guid id, Guid formulaId, [FromBody] ProcessManufacturingFormulaRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new ProcessManufacturingFormulaCommand(id, formulaId, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id:guid}/formula-sources/{formulaId:guid}")]
    public async Task<IActionResult> FormulaSource(Guid id, Guid formulaId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetProductionOrderFormulaSourceQuery(id, formulaId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPatch("{id:guid}/inform")]
    public async Task<IActionResult> UpdateInform(Guid id, [FromBody] ProductionOrderInformRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new UpdateProductionOrderInformCommand(id, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id:guid}/inform")]
    public async Task<IActionResult> Inform(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetProductionOrderInformQuery(id), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/formula")]
    public async Task<IActionResult> CreateFormula(Guid id, [FromBody] CreateProductionOrderFormulaRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new CreateProductionOrderFormulaCommand(id, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id:guid}/workspace")]
    public async Task<IActionResult> Workspace(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetProductionOrderWorkspaceQuery(id), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/use-stock")]
    public async Task<IActionResult> UseStock(Guid id, [FromBody] UseProductionOrderStockRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new UseProductionOrderStockCommand(id, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] GetProductionOrdersQuery query, CancellationToken ct)
    {
        var result = await _sender.Send(query, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetProductionOrderQuery(id), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPatch("{id:guid}/check")]
    public async Task<IActionResult> Check(Guid id, [FromBody] CheckProductionOrderRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new CheckProductionOrderCommand(id, request.IsPrintedStock), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPatch("{id:guid}/finish")]
    public async Task<IActionResult> Finish(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new FinishProductionOrderCommand(id), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPatch("{id:guid}/information")]
    public async Task<IActionResult> UpdateInformation(Guid id, [FromBody] UpdateProductionOrderInformationRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new UpdateProductionOrderInformationCommand(id, request), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("internal")]
    public async Task<ActionResult<OperationResult<Guid>>> CreateInternal(
        [FromBody] CreateInternalProductionOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateInternalProductionOrderCommand(request), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("inform")]
    public async Task<ActionResult<OperationResult<CreateProductionOrderInformResult>>> CreateInform(
        [FromBody] CreateProductionOrderInformRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateProductionOrderInformCommand(request), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Kiểm tra Product đang sản xuất và trả các mã ManufacturingFormula hiện hành liên quan.
    /// </summary>
    [HttpGet("products/{productId:guid}/is-in-production")]
    [ProducesResponseType(typeof(ProductInProductionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductInProductionDto>> IsProductInProduction(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var isInProduction = await _sender.Send(
            new CheckProductInProductionQuery(productId),
            cancellationToken);

        return Ok(isInProduction);
    }
}
