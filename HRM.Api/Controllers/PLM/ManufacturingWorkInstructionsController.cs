using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.PLM.Boms.Commands.ChangeManufacturingWorkInstructionStatus;
using HRM.Application.Features.PLM.Boms.Commands.UpsertManufacturingWorkInstruction;
using HRM.Application.Features.PLM.Boms.Commands.CreateManufacturingWorkInstructionVersion;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Queries.GetManufacturingWorkInstructions;
using HRM.Application.Features.PLM.Boms.Queries.GetManufacturingTemplateOptions;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController, Authorize, Route("api/v1/plm/work-instruction-templates")]
public sealed class ManufacturingWorkInstructionsController : ControllerBase
{
    private readonly ISender _sender;
    public ManufacturingWorkInstructionsController(ISender sender) => _sender = sender;

    [HttpGet, Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetList([FromQuery] ManufacturingTemplateStatus? status, CancellationToken cancellationToken)
        => Ok(await _sender.Send(new GetManufacturingWorkInstructionsQuery(null, status), cancellationToken));

    [HttpGet("options"), Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetOptions([FromQuery] string? keyword, [FromQuery] DateTime? effectiveOn, CancellationToken cancellationToken)
        => Ok(await _sender.Send(new GetManufacturingTemplateOptionsQuery(false, keyword, effectiveOn), cancellationToken));

    [HttpGet("{templateId:guid}"), Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetById(Guid templateId, CancellationToken cancellationToken)
    {
        var values = await _sender.Send(new GetManufacturingWorkInstructionsQuery(templateId, null), cancellationToken);
        return values.Count == 0 ? NotFound() : Ok(values[0]);
    }

    [HttpPost, Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> Create([FromBody] UpsertManufacturingWorkInstructionTemplateRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpsertManufacturingWorkInstructionCommand(null, request), cancellationToken);
        return result.Success ? CreatedAtAction(nameof(GetById), new { templateId = result.Data!.ManufacturingWorkInstructionTemplateId }, result.Data) : BadRequest(result);
    }

    [HttpPut("{templateId:guid}"), Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> Update(Guid templateId, [FromBody] UpsertManufacturingWorkInstructionTemplateRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpsertManufacturingWorkInstructionCommand(templateId, request), cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPost("{templateId:guid}/release"), Authorize(Policy = PlmPolicies.ReleaseBom)]
    public Task<IActionResult> Release(Guid templateId, CancellationToken cancellationToken) => ChangeStatus(templateId, ManufacturingTemplateLifecycleAction.Release, cancellationToken);

    [HttpPost("{templateId:guid}/obsolete"), Authorize(Policy = PlmPolicies.ObsoleteBom)]
    public Task<IActionResult> Obsolete(Guid templateId, CancellationToken cancellationToken) => ChangeStatus(templateId, ManufacturingTemplateLifecycleAction.Obsolete, cancellationToken);

    [HttpPost("{templateId:guid}/new-version"), Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> CreateVersion(Guid templateId, [FromBody] CreateManufacturingTemplateVersionRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateManufacturingWorkInstructionVersionCommand(templateId, request.ChangeReason), cancellationToken);
        return result.Success ? CreatedAtAction(nameof(GetById), new { templateId = result.Data!.ManufacturingWorkInstructionTemplateId }, result.Data) : BadRequest(result);
    }

    private async Task<IActionResult> ChangeStatus(Guid id, ManufacturingTemplateLifecycleAction action, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ChangeManufacturingWorkInstructionStatusCommand(id, action), cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
