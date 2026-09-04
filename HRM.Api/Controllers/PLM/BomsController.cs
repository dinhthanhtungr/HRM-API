using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.PLM.Boms;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

/// <summary>E-BOM master APIs. Only Draft versions may be edited.</summary>
[ApiController]
[Authorize]
[Route("api/v1/plm/boms")]
public sealed class BomsController : ControllerBase
{
    private readonly ISender _sender;
    public BomsController(ISender sender) => _sender = sender;

    [HttpGet]
    [Authorize(Policy = PlmPolicies.ViewFormulaDetail)]
    public async Task<IActionResult> GetList([FromQuery] GetBomsQuery query, CancellationToken ct) => Ok(await _sender.Send(query, ct));

    [HttpGet("versions/{bomVersionId:guid}")]
    [Authorize(Policy = PlmPolicies.ViewFormulaDetail)]
    public async Task<IActionResult> GetVersion(Guid bomVersionId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetBomVersionQuery(bomVersionId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = PlmPolicies.ManageFormula)]
    public async Task<IActionResult> Create([FromBody] CreateBomRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new CreateBomCommand(request), ct);
        return result.Success ? CreatedAtAction(nameof(GetVersion), new { bomVersionId = result.Data!.BomVersionId }, result.Data) : BadRequest(result);
    }

    [HttpPut("versions/{bomVersionId:guid}")]
    [Authorize(Policy = PlmPolicies.ManageFormula)]
    public async Task<IActionResult> Replace(Guid bomVersionId, [FromBody] ReplaceBomVersionRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new ReplaceBomVersionCommand(bomVersionId, request), ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPatch("versions/{bomVersionId:guid}")]
    [Authorize(Policy = PlmPolicies.ManageFormula)]
    public async Task<IActionResult> Patch(Guid bomVersionId, [FromBody] PatchBomVersionRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new PatchBomVersionCommand(bomVersionId, request), ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
