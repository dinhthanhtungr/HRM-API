using HRM.Application.Features.PLM.Formulas.Queries.GetFormulaById;
using HRM.Application.Features.PLM.Formulas.Queries.GetFormulaMaterials;
using HRM.Application.Features.PLM.Formulas.Queries.GetFormulas;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HRM.Application.Commons.Authorization.PLM;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[Route("api/v1/plm/formulas")]
public sealed class FormulasController : ControllerBase
{
    private readonly ISender _sender;

    public FormulasController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] GetFormulasQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{formulaId:guid}")]
    [Authorize(Policy = PlmPolicies.ViewFormulaDetail)]
    public async Task<IActionResult> GetById(
        Guid formulaId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetFormulaByIdQuery
        {
            FormulaId = formulaId
        }, cancellationToken);

        return result is null
            ? NotFound()
            : Ok(result);
    }

    [HttpGet("{formulaId:guid}/materials")]
    [Authorize(Policy = PlmPolicies.ViewFormulaMaterials)]
    public async Task<IActionResult> GetMaterials(
        Guid formulaId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetFormulaMaterialsQuery
        {
            FormulaId = formulaId
        }, cancellationToken);

        return Ok(result);
    }
}
