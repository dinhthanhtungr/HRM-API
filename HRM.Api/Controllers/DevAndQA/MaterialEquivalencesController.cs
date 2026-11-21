using HRM.Application.Features.DevAndQA.MaterialEquivalences;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.DevAndQA;

[ApiController]
[Authorize]
[Route("api/v1/dev/material-equivalences")]
public sealed class MaterialEquivalencesController(ISender sender) : ControllerBase
{
    [HttpGet("demands")]
    public async Task<IActionResult> Demands([FromQuery] GetMaterialDemandsQuery query, CancellationToken ct)
    {
        if (query.Keyword?.Length > 250 || (!string.IsNullOrEmpty(query.ProcessingType) && query.ProcessingType is not ("B" or "N" or "L"))) return BadRequest();
        var result = await sender.Send(query, ct);
        return result is null ? Forbid() : Ok(result);
    }
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] GetMaterialEquivalencesQuery query, CancellationToken ct)
    {
        if (query.StockTypes.Any(x => !Enum.IsDefined(x)) || query.Keyword?.Length > 250 ||
            (!string.IsNullOrEmpty(query.ProcessingType) && query.ProcessingType is not ("B" or "N" or "L")) ||
            (query.ProductOnHandMin.HasValue && query.ProductOnHandMax.HasValue && query.ProductOnHandMin > query.ProductOnHandMax))
            return BadRequest();
        var result = await sender.Send(query, ct);
        return result is null ? Forbid() : Ok(result);
    }
}
