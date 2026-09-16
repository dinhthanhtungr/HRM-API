using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.PLM.Boms.Commands.CreateManufacturingLossType;
using HRM.Application.Features.PLM.Boms.Commands.PatchManufacturingLossType;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Queries.GetManufacturingLossTypes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[Route("api/v1/plm/manufacturing-loss-types")]
public sealed class ManufacturingLossTypesController : ControllerBase
{
    private readonly ISender _sender;

    public ManufacturingLossTypesController(ISender sender) => _sender = sender;

    [HttpGet]
    [Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetList([FromQuery] bool includeInactive, CancellationToken cancellationToken)
        => Ok(await _sender.Send(new GetManufacturingLossTypesQuery(includeInactive), cancellationToken));

    [HttpPost]
    [Authorize(Policy = PlmPolicies.ManageBomLossTypes)]
    public async Task<IActionResult> Create(
        [FromBody] CreateManufacturingLossTypeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateManufacturingLossTypeCommand(request), cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPatch("{manufacturingLossTypeId:guid}")]
    [Authorize(Policy = PlmPolicies.ManageBomLossTypes)]
    public async Task<IActionResult> Patch(
        Guid manufacturingLossTypeId,
        [FromBody] PatchManufacturingLossTypeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new PatchManufacturingLossTypeCommand(manufacturingLossTypeId, request),
            cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
