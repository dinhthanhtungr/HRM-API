using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Commands.ChangeManufacturingProcessTemplateStatus;
using HRM.Application.Features.PLM.Boms.Commands.UpsertManufacturingProcessTemplate;
using HRM.Application.Features.PLM.Boms.Commands.CreateManufacturingProcessTemplateVersion;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Queries.GetManufacturingProcessTemplates;
using HRM.Application.Features.PLM.Boms.Queries.GetManufacturingProcessTemplateEditor;
using HRM.Application.Features.PLM.Boms.Queries.GetManufacturingTemplateOptions;
using HRM.Application.Features.PLM.Boms.Queries.GetManufacturingEquipmentOptions;
using HRM.Application.Features.PLM.Boms.Queries.GetManufacturingEquipmentFilterOptions;
using HRM.Application.Features.PLM.Boms.Queries.GetManufacturingProcessTemplateList;
using HRM.Application.Features.PLM.Boms.Queries.GetManufacturingProcessTemplateSuggestions;
using HRM.Application.Features.PLM.Boms.Queries.ValidateManufacturingProcessTemplateRelease;
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
    public async Task<IActionResult> GetList([FromQuery] GetManufacturingProcessTemplateListQuery query, CancellationToken cancellationToken)
        => Ok(await _sender.Send(query, cancellationToken));

    [HttpGet("options"), Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetOptions([FromQuery] string? keyword, [FromQuery] DateTime? effectiveOn, CancellationToken cancellationToken)
        => Ok(await _sender.Send(new GetManufacturingTemplateOptionsQuery(true, keyword, effectiveOn), cancellationToken));

    [HttpGet("suggestions"), Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetSuggestions(
        [FromQuery] Guid formulaId,
        [FromQuery] DateTime? effectiveOn,
        CancellationToken cancellationToken)
        => Ok(await _sender.Send(
            new GetManufacturingProcessTemplateSuggestionsQuery(formulaId, null, effectiveOn),
            cancellationToken));

    [HttpGet("equipment-options"), Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetEquipmentOptions(
        [FromQuery] string? keyword,
        [FromQuery] string? groupType,
        [FromQuery] string? areaExternalId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(await _sender.Send(
            new GetManufacturingEquipmentOptionsQuery(keyword, groupType, areaExternalId, page, pageSize),
            cancellationToken));

    [HttpGet("equipment-filter-options"), Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetEquipmentFilterOptions(CancellationToken cancellationToken)
        => Ok(await _sender.Send(new GetManufacturingEquipmentFilterOptionsQuery(), cancellationToken));

    [HttpGet("{templateId:guid}"), Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetById(Guid templateId, CancellationToken cancellationToken)
    {
        var values = await _sender.Send(new GetManufacturingProcessTemplatesQuery(templateId, null), cancellationToken);
        return values.Count == 0 ? NotFound() : Ok(values[0]);
    }

    [HttpGet("{templateId:guid}/editor"), Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetEditor(Guid templateId, CancellationToken cancellationToken)
    {
        var value = await _sender.Send(new GetManufacturingProcessTemplateEditorQuery(templateId), cancellationToken);
        return value is null ? NotFound() : Ok(value);
    }

    [HttpPost, Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> Create([FromBody] UpsertManufacturingProcessTemplateRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpsertManufacturingProcessTemplateCommand(null, request), cancellationToken);
        return result.Success
            ? CreatedAtAction(nameof(GetEditor), new { templateId = result.Data!.ManufacturingProcessTemplateId }, result.Data)
            : MutationFailure(result);
    }

    [HttpPut("{templateId:guid}"), Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> Update(Guid templateId, [FromBody] UpsertManufacturingProcessTemplateRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpsertManufacturingProcessTemplateCommand(templateId, request), cancellationToken);
        return result.Success ? Ok(result.Data) : MutationFailure(result);
    }

    [HttpPost("{templateId:guid}/release"), Authorize(Policy = PlmPolicies.ReleaseBom)]
    public Task<IActionResult> Release(Guid templateId, CancellationToken cancellationToken) => ChangeStatus(templateId, ManufacturingTemplateLifecycleAction.Release, cancellationToken);

    [HttpGet("{templateId:guid}/release-validation"), Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> ValidateRelease(Guid templateId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ValidateManufacturingProcessTemplateReleaseQuery(templateId), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

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

    private ActionResult MutationFailure(OperationResult result)
        => OptimisticConcurrencyHelper.IsConflictMessage(result.Message)
            ? Conflict(result)
            : BadRequest(result);
}
