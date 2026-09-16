using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.PLM.Boms.Commands.FinalizeProductionOrderLosses;
using HRM.Application.Features.PLM.Boms.Commands.InitializeProductionOrderLosses;
using HRM.Application.Features.PLM.Boms.Commands.PatchProductionOrderLoss;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Queries.GetProductionOrderLosses;
using HRM.Application.Features.PLM.Boms.Queries.GetProductionMaterialRequirements;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[Route("api/v1/plm/production-orders/{productionOrderId:guid}/losses")]
public sealed class ProductionOrderLossesController : ControllerBase
{
    private readonly ISender _sender;

    public ProductionOrderLossesController(ISender sender) => _sender = sender;

    [HttpGet]
    [Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> Get(Guid productionOrderId, CancellationToken cancellationToken)
        => Ok(await _sender.Send(new GetProductionOrderLossesQuery(productionOrderId), cancellationToken));

    [HttpPost("initialize")]
    [Authorize(Policy = PlmPolicies.UpdateProductionLoss)]
    public async Task<IActionResult> Initialize(Guid productionOrderId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new InitializeProductionOrderLossesCommand(productionOrderId), cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPatch("{lossId:guid}")]
    [Authorize(Policy = PlmPolicies.UpdateProductionLoss)]
    public async Task<IActionResult> Patch(
        Guid productionOrderId,
        Guid lossId,
        [FromBody] PatchProductionOrderLossRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new PatchProductionOrderLossCommand(productionOrderId, lossId, request),
            cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPost("finalize")]
    [Authorize(Policy = PlmPolicies.FinalizeProductionLoss)]
    public async Task<IActionResult> Finalize(Guid productionOrderId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new FinalizeProductionOrderLossesCommand(productionOrderId), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("material-requirements")]
    [Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetMaterialRequirements(
        Guid productionOrderId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetProductionMaterialRequirementsQuery(productionOrderId),
            cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
