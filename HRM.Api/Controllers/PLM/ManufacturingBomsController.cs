using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.PLM.Boms.Commands.CreateManufacturingBom;
using HRM.Application.Features.PLM.Boms.Commands.ApplyManufacturingProcessTemplate;
using HRM.Application.Features.PLM.Boms.Commands.ApplyManufacturingLossProfile;
using HRM.Application.Features.PLM.Boms.Commands.PreviewManufacturingLossProfile;
using HRM.Application.Features.PLM.Boms.Commands.ReplaceManufacturingBom;
using HRM.Application.Features.PLM.Boms.Commands.UpdateManufacturingBomProcessConfiguration;
using HRM.Application.Features.PLM.Boms.Commands.GenerateManufacturingFormulaFromBom;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Queries.GetBomVersion;
using HRM.Application.Features.PLM.Boms.Queries.GetBomDefinition;
using HRM.Application.Features.PLM.Boms.Queries.GetBoms;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[Route("api/v1/plm/manufacturing-boms")]
public sealed class ManufacturingBomsController : ControllerBase
{
    private readonly ISender _sender;

    public ManufacturingBomsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetMany(
        [FromQuery] Guid? productId,
        [FromQuery] string? keyword,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetBomsQuery
        {
            ProductId = productId,
            Keyword = keyword,
            BomType = BomType.Manufacturing
        }, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{bomDefinitionId:guid}")]
    [Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetDefinition(
        Guid bomDefinitionId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetBomDefinitionQuery(bomDefinitionId, BomType.Manufacturing),
            cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("from-engineering/{engineeringBomVersionId:guid}")]
    [Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> CreateFromEngineering(
        Guid engineeringBomVersionId,
        [FromBody] CreateManufacturingBomRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreateManufacturingBomCommand(engineeringBomVersionId, request),
            cancellationToken);
        return result.Success
            ? CreatedAtAction(nameof(GetVersion), new { bomVersionId = result.Data!.BomVersionId }, result.Data)
            : BadRequest(result);
    }

    [HttpGet("versions/{bomVersionId:guid}")]
    [Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetVersion(Guid bomVersionId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetBomVersionQuery(bomVersionId, BomType.Manufacturing),
            cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("versions/{bomVersionId:guid}")]
    [Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> Replace(
        Guid bomVersionId,
        [FromBody] ReplaceManufacturingBomRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ReplaceManufacturingBomCommand(bomVersionId, request),
            cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPut("versions/{bomVersionId:guid}/process-configuration")]
    [Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> UpdateProcessConfiguration(
        Guid bomVersionId,
        [FromBody] UpdateManufacturingBomProcessConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateManufacturingBomProcessConfigurationCommand(bomVersionId, request),
            cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPost("versions/{bomVersionId:guid}/loss-rules/preview-profile")]
    [Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> PreviewLossProfile(
        Guid bomVersionId,
        [FromBody] ApplyManufacturingLossProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new PreviewManufacturingLossProfileCommand(bomVersionId, request.ProfileId),
            cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPost("versions/{bomVersionId:guid}/process-template/preview")]
    [Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> PreviewProcessTemplate(Guid bomVersionId, [FromBody] ApplyManufacturingProcessTemplateRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ApplyManufacturingProcessTemplateCommand(bomVersionId, request.ProcessTemplateId, true), cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPost("versions/{bomVersionId:guid}/process-template/apply")]
    [Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> ApplyProcessTemplate(Guid bomVersionId, [FromBody] ApplyManufacturingProcessTemplateRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ApplyManufacturingProcessTemplateCommand(bomVersionId, request.ProcessTemplateId, false), cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPost("versions/{bomVersionId:guid}/loss-rules/apply-profile")]
    [Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> ApplyLossProfile(
        Guid bomVersionId,
        [FromBody] ApplyManufacturingLossProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ApplyManufacturingLossProfileCommand(bomVersionId, request.ProfileId),
            cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPost("versions/{bomVersionId:guid}/manufacturing-formulas")]
    [Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> GenerateManufacturingFormula(
        Guid bomVersionId,
        [FromBody] GenerateManufacturingFormulaFromBomRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GenerateManufacturingFormulaFromBomCommand(bomVersionId, request),
            cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
