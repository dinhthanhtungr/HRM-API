using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.PLM.ColorChipRecords.Commands.CreateColorChipRecord;
using HRM.Application.Features.PLM.ColorChipRecords.Commands.PatchColorChipRecord;
using HRM.Application.Features.PLM.ColorChipRecords.Queries.GetColorChipRecordById;
using HRM.Application.Features.PLM.ColorChipRecords.Queries.GetColorChipRecordByProductId;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[Route("api/v1/plm/color-chip-records")]
public sealed class ColorChipRecordsController : ControllerBase
{
    private readonly ISender _sender;

    public ColorChipRecordsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("{colorChipRecordId:guid}")]
    public async Task<IActionResult> GetById(Guid colorChipRecordId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetColorChipRecordByIdQuery(colorChipRecordId),
            cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("by-product/{productId:guid}")]
    public async Task<IActionResult> GetByProductId(Guid productId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetColorChipRecordByProductIdQuery(productId),
            cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = PlmPolicies.EditProductTechnicalInfo)]
    public async Task<IActionResult> Create(
        [FromBody] CreateColorChipRecordCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        return result.Success
            ? CreatedAtAction(
                nameof(GetById),
                new { colorChipRecordId = result.Data!.ColorChipRecordId },
                result)
            : BadRequest(result);
    }

    [HttpPatch("{colorChipRecordId:guid}")]
    [Authorize(Policy = PlmPolicies.EditProductTechnicalInfo)]
    public async Task<IActionResult> Patch(
        Guid colorChipRecordId,
        [FromBody] PatchColorChipRecordCommand command,
        CancellationToken cancellationToken)
    {
        command.ColorChipRecordId = colorChipRecordId;
        var result = await _sender.Send(command, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
