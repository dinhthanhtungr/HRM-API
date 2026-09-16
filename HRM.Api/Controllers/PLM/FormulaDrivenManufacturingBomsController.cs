using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.PLM.Boms.Commands.CreateManufacturingBomFromSelectedFormula;
using HRM.Application.Features.PLM.Boms.Queries.GetFormulaDrivenManufacturingBomQueue;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

/// <summary>
/// Additive Formula-to-M-BOM flow. This controller does not change existing E-BOM or M-BOM routes.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/plm/formula-driven-manufacturing-boms")]
public sealed class FormulaDrivenManufacturingBomsController : ControllerBase
{
    private readonly ISender _sender;

    public FormulaDrivenManufacturingBomsController(ISender sender) => _sender = sender;

    [HttpGet]
    [Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetQueue(CancellationToken cancellationToken)
        => Ok(await _sender.Send(new GetFormulaDrivenManufacturingBomQueueQuery(), cancellationToken));

    /// <summary>
    /// Creates, or returns the existing, M-BOM Draft snapshot for the customer's selected completed Formula.
    /// Formula materials are copied as a snapshot; this endpoint does not alter existing BOM APIs or data.
    /// </summary>
    [HttpPost("from-selected-formula/{productId:guid}")]
    [Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> CreateFromSelectedFormula(Guid productId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreateManufacturingBomFromSelectedFormulaCommand(productId),
            cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return result.Data!.IsExistingSnapshot
            ? Ok(result.Data)
            : Created(
                $"/api/v1/plm/manufacturing-boms/versions/{result.Data.BomVersionId}",
                result.Data);
    }
}
