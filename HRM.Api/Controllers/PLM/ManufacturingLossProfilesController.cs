using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.PLM.Boms.Commands.CreateManufacturingLossProfile;
using HRM.Application.Features.PLM.Boms.Commands.ObsoleteManufacturingLossProfile;
using HRM.Application.Features.PLM.Boms.Commands.ReleaseManufacturingLossProfile;
using HRM.Application.Features.PLM.Boms.Commands.UpdateManufacturingLossProfile;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Queries.GetManufacturingLossProfileById;
using HRM.Application.Features.PLM.Boms.Queries.GetManufacturingLossProfiles;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[Route("api/v1/plm/manufacturing-loss-profiles")]
public sealed class ManufacturingLossProfilesController : ControllerBase
{
    private readonly ISender _sender;

    public ManufacturingLossProfilesController(ISender sender) => _sender = sender;

    [HttpGet]
    [Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetList(
        [FromQuery] ManufacturingLossProfileStatus? status,
        CancellationToken cancellationToken)
        => Ok(await _sender.Send(new GetManufacturingLossProfilesQuery(status), cancellationToken));

    [HttpPost]
    [Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> Create(
        [FromBody] UpsertManufacturingLossProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateManufacturingLossProfileCommand(request), cancellationToken);
        return result.Success
            ? CreatedAtAction(nameof(GetById), new { profileId = result.Data!.ManufacturingLossProfileId }, result.Data)
            : BadRequest(result);
    }

    [HttpGet("{profileId:guid}")]
    [Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetById(Guid profileId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetManufacturingLossProfileByIdQuery(profileId), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{profileId:guid}")]
    [Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> Update(
        Guid profileId,
        [FromBody] UpsertManufacturingLossProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateManufacturingLossProfileCommand(profileId, request), cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPost("{profileId:guid}/release")]
    [Authorize(Policy = PlmPolicies.ReleaseBom)]
    public async Task<IActionResult> Release(Guid profileId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ReleaseManufacturingLossProfileCommand(profileId), cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPost("{profileId:guid}/obsolete")]
    [Authorize(Policy = PlmPolicies.ObsoleteBom)]
    public async Task<IActionResult> Obsolete(Guid profileId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ObsoleteManufacturingLossProfileCommand(profileId), cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
