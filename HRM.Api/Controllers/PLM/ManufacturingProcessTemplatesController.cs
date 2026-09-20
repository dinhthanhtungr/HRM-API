using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.PLM.Boms.Commands.ChangeManufacturingProcessTemplateStatus;
using HRM.Application.Features.PLM.Boms.Commands.UpsertManufacturingProcessTemplate;
using HRM.Application.Features.PLM.Boms.Commands.CreateManufacturingProcessTemplateVersion;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Queries.GetManufacturingProcessTemplates;
using HRM.Application.Features.PLM.Boms.Queries.GetManufacturingTemplateOptions;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController, Authorize, Route("api/v1/plm/process-templates")]
public sealed class ManufacturingProcessTemplatesController : ControllerBase
{
    private readonly ISender _sender;
    public ManufacturingProcessTemplatesController(ISender sender) => _sender = sender;

    [HttpGet, Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetList([FromQuery] ManufacturingTemplateStatus? status, CancellationToken cancellationToken)
        => Ok(await _sender.Send(new GetManufacturingProcessTemplatesQuery(null, status), cancellationToken));

    [HttpGet("options"), Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetOptions([FromQuery] string? keyword, [FromQuery] DateTime? effectiveOn, CancellationToken cancellationToken)
        => Ok(await _sender.Send(new GetManufacturingTemplateOptionsQuery(true, keyword, effectiveOn), cancellationToken));

    [HttpGet("{templateId:guid}"), Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetById(Guid templateId, CancellationToken cancellationToken)
    {
        var values = await _sender.Send(new GetManufacturingProcessTemplatesQuery(templateId, null), cancellationToken);
        return values.Count == 0 ? NotFound() : Ok(values[0]);
    }

    [HttpPost, Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> Create([FromBody] UpsertManufacturingProcessTemplateRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpsertManufacturingProcessTemplateCommand(null, request), cancellationToken);
        return result.Success ? CreatedAtAction(nameof(GetById), new { templateId = result.Data!.ManufacturingProcessTemplateId }, result.Data) : BadRequest(result);
    }

    [HttpPut("{templateId:guid}"), Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> Update(Guid templateId, [FromBody] UpsertManufacturingProcessTemplateRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpsertManufacturingProcessTemplateCommand(templateId, request), cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPost("{templateId:guid}/release"), Authorize(Policy = PlmPolicies.ReleaseBom)]
    public Task<IActionResult> Release(Guid templateId, CancellationToken cancellationToken) => ChangeStatus(templateId, ManufacturingTemplateLifecycleAction.Release, cancellationToken);

    [HttpPost("{templateId:guid}/obsolete"), Authorize(Policy = PlmPolicies.ObsoleteBom)]
    public Task<IActionResult> Obsolete(Guid templateId, CancellationToken cancellationToken) => ChangeStatus(templateId, ManufacturingTemplateLifecycleAction.Obsolete, cancellationToken);

    [HttpPost("{templateId:guid}/new-version"), Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> CreateVersion(Guid templateId, [FromBody] CreateManufacturingTemplateVersionRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateManufacturingProcessTemplateVersionCommand(templateId, request.ChangeReason), cancellationToken);
        return result.Success ? CreatedAtAction(nameof(GetById), new { templateId = result.Data!.ManufacturingProcessTemplateId }, result.Data) : BadRequest(result);
    }

    private async Task<IActionResult> ChangeStatus(Guid id, ManufacturingTemplateLifecycleAction action, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ChangeManufacturingProcessTemplateStatusCommand(id, action), cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
