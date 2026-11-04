using HRM.Application.Commons.Models;
using HRM.Application.Features.DevAndQA.IQC.Queries.ExportIqcEvaluationExcel;
using HRM.Application.Features.DevAndQA.Shared.Evaluations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HRM.Api.Controllers.DevAndQA;
[ApiController]
[Authorize]
[Route("api/v1/dev-and-qa/iqc/{voucherDetailId:long}/evaluation/export-excel")]
public sealed class IqcEvaluationExportsController(ISender sender) : ControllerBase
{
    /// <summary>Xuất phiếu đã lưu và một trang lịch sử; quyền/company được kiểm tra trong Application.</summary>
    [HttpGet]
    public async Task<IActionResult> Export(long voucherDetailId, CancellationToken ct,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 3)
    {
        try
        {
            var file = await sender.Send(new ExportIqcEvaluationExcelQuery(voucherDetailId, pageNumber, pageSize), ct);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (EvaluationRequestException ex)
        {
            return StatusCode(ex.StatusCode, OperationResult.Fail(ex.Message));
        }
    }
}
