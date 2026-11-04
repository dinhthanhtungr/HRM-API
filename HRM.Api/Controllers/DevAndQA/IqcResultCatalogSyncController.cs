using System.Data.Common;
using HRM.Application.Commons.Models;
using HRM.Application.Features.DevAndQA.IQC.Commands.SyncIqcResultsFromCatalog;
using HRM.Application.Features.DevAndQA.IQC.Queries.PreviewIqcResultCatalogSync;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.DevAndQA;

/// <summary>Đồng bộ snapshot đã lưu theo scope; không dùng route voucher để tránh nhầm QC ID.</summary>
[Authorize]
[ApiController]
[Route("api/v1/dev-and-qa/iqc/evaluation-results")]
public sealed class IqcResultCatalogSyncController(ISender sender) : EvaluationControllerBase(sender)
{
    [HttpPost("catalog-sync-preview")]
    public Task<IActionResult> Preview(PreviewIqcResultCatalogSyncQuery request, CancellationToken ct) => ExecuteSync(request, ct);

    [HttpPost("sync-from-catalog")]
    public Task<IActionResult> Sync(SyncIqcResultsFromCatalogCommand request, CancellationToken ct) => ExecuteSync(request, ct);
    private async Task<IActionResult> ExecuteSync<T>(IRequest<T> request, CancellationToken ct)
    {
        try { return await Execute(request, ct); }
        catch (DbException) { return Conflict(OperationResult.Fail("Dữ liệu đang thay đổi hoặc transaction chưa xác định. Hãy tải lại và xem trước lại.")); }
    }
}

