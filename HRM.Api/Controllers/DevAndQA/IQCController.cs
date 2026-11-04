using HRM.Application.Features.DevAndQA.IQC.Commands.CreateIqc;
using HRM.Application.Features.DevAndQA.IQC.Commands.PatchIqc;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Application.Features.DevAndQA.IQC.Queries.ExportIqcExcel;
using HRM.Application.Features.DevAndQA.IQC.Queries.GetIqcDetail;
using HRM.Application.Features.DevAndQA.IQC.Queries.GetIqcSummary;
using MediatR;
using HRM.Application.Commons.Models;
using HRM.Application.Features.DevAndQA.IQC.Commands.RequestPurchaseOrderExchange;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.DevAndQA;

[ApiController]
[Authorize]
[Route("api/v1/dev-and-qa/iqc")]
[Route("api/iqcinput")]
public sealed class IQCController(ISender sender) : ControllerBase
{
    /// <summary>Yêu cầu đổi hàng đối với DDH liên quan phiếu IQC; không cần body.</summary>
    [HttpPost("{voucherDetailId:long}/purchase-order/request-exchange")]
    public async Task<IActionResult> RequestExchange(long voucherDetailId, CancellationToken ct)
    {
        try
        {
            var result = await sender.Send(new RequestPurchaseOrderExchangeCommand(voucherDetailId), ct);
            return Ok(OperationResult<PurchaseOrderExchangeDto>.Ok(result));
        }
        catch (EvaluationRequestException ex)
        {
            return StatusCode(ex.StatusCode, OperationResult.Fail(ex.Message));
        }
    }

    [HttpGet]
    [HttpGet("GetPaged")]
    public async Task<IActionResult> GetPaged([FromQuery] QCInputQuery query, CancellationToken ct)
    {
        var result = await sender.Send(new GetIqcSummaryQuery(query), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("GetByVoucherDetailId")]
    public async Task<IActionResult> GetDetail([FromQuery] long voucherDetailId, CancellationToken ct)
    {
        var result = await sender.Send(new GetIqcDetailQuery(voucherDetailId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{voucherDetailId:long}")]
    public async Task<IActionResult> GetById(long voucherDetailId, CancellationToken ct) =>
        await GetDetail(voucherDetailId, ct);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PostQCInputByQC input, CancellationToken ct)
    {
        var result = await sender.Send(new CreateIqcCommand(input), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPatch("{voucherDetailId:long}")]
    [HttpPatch("PatchByVoucherDetailId/{voucherDetailId:long}")]
    public async Task<IActionResult> Patch(long voucherDetailId, [FromBody] PatchQCInputByQC input, CancellationToken ct)
    {
        var result = await sender.Send(new PatchIqcCommand(voucherDetailId, input), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("export-excel")]
    public async Task<IActionResult> ExportExcel([FromQuery] QCInputQuery query, CancellationToken ct)
    {
        var result = await sender.Send(new ExportIqcExcelQuery(query), ct);
        if (!result.Success || result.Data is null) return BadRequest(result);
        return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }
}
