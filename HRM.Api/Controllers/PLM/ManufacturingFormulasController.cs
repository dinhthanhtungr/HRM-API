using HRM.Application.Features.PLM.Formulas.Queries.GetManufacturingFormulaMaterials;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HRM.Application.Commons.Authorization.PLM;

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

        return Ok(result);
    }
}
