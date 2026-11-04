using HRM.Application.Features.DevAndQA.IQC.Queries.GetIqcEvaluation;
using HRM.Application.Features.DevAndQA.IQC.Queries.GetIqcEvaluationHistory;
using HRM.Application.Features.DevAndQA.IQC.Commands.InitializeIqcEvaluation;
using HRM.Application.Features.DevAndQA.IQC.Commands.AddIqcEvaluationResult;
using HRM.Application.Features.DevAndQA.IQC.Commands.PatchIqcEvaluationResult;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.DevAndQA;

[ApiController]
[Authorize]
[Route("api/v1/dev-and-qa/iqc/{voucherDetailId:long}/evaluation")]
public sealed class IqcEvaluationsController(ISender sender) : EvaluationControllerBase(sender)
{
    [HttpGet]
    public Task<IActionResult> Detail(long voucherDetailId, CancellationToken ct) => Execute(new GetIqcEvaluationQuery(voucherDetailId), ct);
    [HttpGet("history")]
    public Task<IActionResult> History(long voucherDetailId, CancellationToken ct,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 3)
        => Execute(new GetIqcEvaluationHistoryQuery(voucherDetailId, pageNumber, pageSize), ct);
    [HttpPost]
    public Task<IActionResult> Initialize(long voucherDetailId, InitializeIqcEvaluationCommand request, CancellationToken ct) => Execute(request with { VoucherDetailId = voucherDetailId }, ct);
    [HttpPost("results")]
    public Task<IActionResult> Add(long voucherDetailId, AddIqcEvaluationResultCommand request, CancellationToken ct) => Execute(request with { VoucherDetailId = voucherDetailId }, ct, true);
    [HttpPatch("results/{resultId:guid}")]
    public Task<IActionResult> Patch(long voucherDetailId, Guid resultId, PatchIqcEvaluationResultCommand request, CancellationToken ct) => Execute(request with { VoucherDetailId = voucherDetailId, ResultId = resultId }, ct);
}
