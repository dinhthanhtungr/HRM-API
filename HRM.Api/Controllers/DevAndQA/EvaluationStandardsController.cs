using HRM.Application.Features.DevAndQA.IQC.Commands.CreateEvaluationStandard;
using HRM.Application.Features.DevAndQA.IQC.Commands.PatchEvaluationStandard;
using HRM.Application.Features.DevAndQA.IQC.Queries.GetEvaluationStandardDetail;
using HRM.Application.Features.DevAndQA.IQC.Queries.GetEvaluationStandards;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.DevAndQA;

/// <summary>Danh mục tiêu chuẩn đánh giá dùng cho IQC, có company scope và capability.</summary>
[ApiController]
[Authorize]
[Route("api/v1/dev-and-qa/evaluation-standards")]
[Route("api/v1/dev-and-qa/iqc/evaluation-standards")]
public sealed class EvaluationStandardsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] GetEvaluationStandardsQuery query, CancellationToken ct)
    {
        var result = await sender.Send(query, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetEvaluationStandardDetailQuery(id), ct);
        if (!result.Success) return BadRequest(result);
        return result.Data is null ? NotFound(result) : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEvaluationStandardCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return result.Success ? CreatedAtAction(nameof(Detail), new { id = result.Data!.Id }, result) : BadRequest(result);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Patch(Guid id, [FromBody] PatchEvaluationStandardCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command with { Id = id }, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
