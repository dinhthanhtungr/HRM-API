using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.PLM.Boms.Commands.CreateBom;
using HRM.Application.Features.PLM.Boms.Commands.CreateBomFromSelectedFormula;
using HRM.Application.Features.PLM.Boms.Commands.CreateBomVersion;
using HRM.Application.Features.PLM.Boms.Commands.ObsoleteBomVersion;
using HRM.Application.Features.PLM.Boms.Commands.PatchBomVersion;
using HRM.Application.Features.PLM.Boms.Commands.ReleaseBomVersion;
using HRM.Application.Features.PLM.Boms.Commands.ReplaceBomVersion;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Queries.GetBomDefinition;
using HRM.Application.Features.PLM.Boms.Queries.ExplodeBom;
using HRM.Application.Features.PLM.Boms.Queries.GetBoms;
using HRM.Application.Features.PLM.Boms.Queries.GetBomVersion;
using HRM.Domain.Enums.Boms;
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
    [Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetList(
        [FromQuery] Guid? productId,
        [FromQuery] string? keyword,
        CancellationToken ct)
        => Ok(await _sender.Send(new GetBomsQuery
        {
            ProductId = productId,
            Keyword = keyword
        }, ct));

    [HttpGet("versions/{bomVersionId:guid}")]
    [Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetVersion(Guid bomVersionId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetBomVersionQuery(bomVersionId, BomType.Engineering), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> Create([FromBody] CreateBomRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new CreateBomCommand(request), ct);
        return result.Success ? CreatedAtAction(nameof(GetVersion), new { bomVersionId = result.Data!.BomVersionId }, result.Data) : BadRequest(result);
    }

    /// <summary>
    /// Tạo E-BOM Draft đầu tiên của Product từ Formula đã được khách hàng chọn/chốt.
    /// </summary>
    [HttpPost("from-selected-formula/{productId:guid}")]
    [Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> CreateFromSelectedFormula(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreateBomFromSelectedFormulaCommand(productId),
            cancellationToken);
        return result.Success
            ? CreatedAtAction(nameof(GetVersion), new { bomVersionId = result.Data!.BomVersionId }, result.Data)
            : BadRequest(result);
    }

    [HttpPut("versions/{bomVersionId:guid}")]
    [Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> Replace(Guid bomVersionId, [FromBody] ReplaceBomVersionRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new ReplaceBomVersionCommand(bomVersionId, request), ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPatch("versions/{bomVersionId:guid}")]
    [Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> Patch(Guid bomVersionId, [FromBody] PatchBomVersionRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new PatchBomVersionCommand(bomVersionId, request), ct);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("{bomDefinitionId:guid}")]
    [Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> GetDefinition(Guid bomDefinitionId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetBomDefinitionQuery(bomDefinitionId, BomType.Engineering), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{bomDefinitionId:guid}/versions")]
    [Authorize(Policy = PlmPolicies.ManageBomDraft)]
    public async Task<IActionResult> CreateVersion(
        Guid bomDefinitionId,
        [FromBody] CreateBomVersionRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(new CreateBomVersionCommand(bomDefinitionId, request), ct);
        return result.Success
            ? CreatedAtAction(nameof(GetVersion), new { bomVersionId = result.Data!.BomVersionId }, result.Data)
            : BadRequest(result);
    }

    [HttpPost("versions/{bomVersionId:guid}/release")]
    [Authorize(Policy = PlmPolicies.ReleaseBom)]
    public async Task<IActionResult> Release(Guid bomVersionId, CancellationToken ct)
    {
        var result = await _sender.Send(new ReleaseBomVersionCommand(bomVersionId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("versions/{bomVersionId:guid}/obsolete")]
    [Authorize(Policy = PlmPolicies.ObsoleteBom)]
    public async Task<IActionResult> Obsolete(
        Guid bomVersionId,
        [FromBody] ObsoleteBomVersionRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(new ObsoleteBomVersionCommand(bomVersionId, request.Reason), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("versions/{bomVersionId:guid}/explosion")]
    [Authorize(Policy = PlmPolicies.ViewBom)]
    public async Task<IActionResult> Explode(
        Guid bomVersionId,
        [FromQuery] decimal outputQuantity,
        [FromQuery] int maxDepth = 12,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new ExplodeBomQuery(bomVersionId, outputQuantity, maxDepth),
            cancellationToken);
        return result.Success ? Ok(result.Data) : BadRequest(result);
    }
}
