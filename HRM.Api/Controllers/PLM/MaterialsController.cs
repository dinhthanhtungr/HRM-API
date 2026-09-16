using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.PLM.Materials.Commands.UpdateMaterialPurchaseAvailability;
using HRM.Application.Features.PLM.Materials.Commands.CreateMaterialReplacement;
using HRM.Application.Features.PLM.Materials.Commands.UpdateMaterialReplacement;
using HRM.Application.Features.PLM.Materials.Dtos;
using HRM.Application.Features.PLM.Materials.Queries.GetMaterialReplacementOptions;
using HRM.Application.Features.PLM.Materials.Queries.GetMaterialReplacements;
using HRM.Application.Features.PLM.Materials.Queries.GetMaterialAttachmentContent;
using HRM.Application.Features.PLM.Materials.Queries.GetMaterialPreview;
using HRM.Application.Features.PLM.Materials.DocumentImport.Preview;
using HRM.Application.Features.PLM.Materials.DocumentImport.Jobs;
using HRM.Domain.Security.Rules.Roles;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[Route("api/v1/plm/materials")]
public sealed class MaterialsController : ControllerBase
{
    private readonly ISender _sender;

    public MaterialsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Lab và PLPU xem các NVL thay thế đang hiệu lực, còn mua được, để tự chọn khi cập nhật công thức.
    /// </summary>
    [HttpGet("{materialId:guid}/replacement-options")]
    [Authorize(Policy = PlmPolicies.ViewMaterialReplacements)]
    public async Task<IActionResult> GetReplacementOptions(Guid materialId, CancellationToken cancellationToken)
        => Ok(await _sender.Send(new GetMaterialReplacementOptionsQuery(materialId), cancellationToken));

    /// <summary>
    /// PLPU quản lý cả phương án đã ngưng hoặc NVL thay thế hiện không khả dụng.
    /// </summary>
    [HttpGet("{materialId:guid}/replacements")]
    [Authorize(Policy = PlmPolicies.ManageMaterialReplacements)]
    public async Task<IActionResult> GetReplacements(Guid materialId, CancellationToken cancellationToken)
        => Ok(await _sender.Send(new GetMaterialReplacementsQuery(materialId), cancellationToken));

    [HttpPost("{materialId:guid}/replacements")]
    [Authorize(Policy = PlmPolicies.ManageMaterialReplacements)]
    public async Task<IActionResult> CreateReplacement(
        Guid materialId,
        [FromBody] CreateMaterialReplacementRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateMaterialReplacementCommand(materialId, request), cancellationToken);
        return result.Outcome switch
        {
            MaterialReplacementWriteOutcome.Created => CreatedAtAction(
                nameof(GetReplacements), new { materialId }, result.Data),
            MaterialReplacementWriteOutcome.Updated => Ok(result.Data),
            MaterialReplacementWriteOutcome.NotFound => NotFound(),
            _ => BadRequest(new { result.Message })
        };
    }

    [HttpPut("replacements/{materialReplacementId:guid}")]
    [Authorize(Policy = PlmPolicies.ManageMaterialReplacements)]
    public async Task<IActionResult> UpdateReplacement(
        Guid materialReplacementId,
        [FromBody] UpdateMaterialReplacementRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateMaterialReplacementCommand(materialReplacementId, request), cancellationToken);
        return result.Outcome switch
        {
            MaterialReplacementWriteOutcome.Updated => Ok(result.Data),
            MaterialReplacementWriteOutcome.NotFound => NotFound(),
            _ => BadRequest(new { result.Message })
        };
    }

    /// <summary>
    /// Kế hoạch đánh dấu khả năng mua NVL. Lần chuyển sang Unavailable sẽ thông báo cho Lab.
    /// </summary>
    [HttpPut("{materialId:guid}/purchase-availability")]
    [Authorize(Policy = PlmPolicies.ManageMaterialPurchaseAvailability)]
    public async Task<IActionResult> UpdatePurchaseAvailability(
        Guid materialId,
        [FromBody] UpdateMaterialPurchaseAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateMaterialPurchaseAvailabilityCommand(materialId, request),
            cancellationToken);

        return result.Outcome switch
        {
            UpdateMaterialPurchaseAvailabilityOutcome.Updated => Ok(result.Data),
            UpdateMaterialPurchaseAvailabilityOutcome.NotFound => NotFound(),
            _ => BadRequest(new { result.Message })
        };
    }

    /// <summary>
    /// Quét thử thư mục TDS/MSDS được cấu hình và đối chiếu mã file với NVL
    /// active trong công ty hiện tại. API chỉ đọc metadata, không import file.
    /// </summary>
    [HttpGet("document-import/preview")]
    [Authorize(Roles = RoleSets.Admins)]
    public async Task<IActionResult> PreviewDocumentImport(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new PreviewMaterialDocumentImportQuery(),
            cancellationToken);

        return result is null ? Forbid() : Ok(result);
    }

    /// <summary>
    /// Tạo background job tự động import mọi file có đúng một mã NVL khớp
    /// duy nhất trong công ty hiện tại. File không khớp được đưa vào exceptions.
    /// </summary>
    [HttpPost("document-import/jobs")]
    [Authorize(Roles = RoleSets.Admins)]
    public async Task<IActionResult> StartDocumentImportJob(
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new StartMaterialDocumentImportJobCommand(),
            cancellationToken);
        if (result is null)
        {
            return Forbid();
        }

        if (!result.Accepted)
        {
            return Conflict(result.Job);
        }

        return AcceptedAtAction(
            nameof(GetDocumentImportJob),
            new { jobId = result.Job.JobId },
            result.Job);
    }

    /// <summary>
    /// Lấy tiến độ và tổng hợp kết quả của background job import tài liệu NVL.
    /// </summary>
    [HttpGet("document-import/jobs/{jobId:guid}")]
    [Authorize(Roles = RoleSets.Admins)]
    public async Task<IActionResult> GetDocumentImportJob(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetMaterialDocumentImportJobQuery(jobId),
            cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Lấy các file cần kiểm tra tay hoặc bị lỗi trong một job import.
    /// </summary>
    [HttpGet("document-import/jobs/{jobId:guid}/exceptions")]
    [Authorize(Roles = RoleSets.Admins)]
    public async Task<IActionResult> GetDocumentImportJobExceptions(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetMaterialDocumentImportJobExceptionsQuery(jobId),
            cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Trả thông tin NVL và metadata tệp đính kèm để UI hiển thị xem nhanh.
    /// </summary>
    [HttpGet("{materialId:guid}/preview")]
    public async Task<IActionResult> GetPreview(
        Guid materialId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetMaterialPreviewQuery(materialId),
            cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Stream tệp đính kèm sau khi kiểm tra tệp thuộc NVL trong công ty hiện tại.
    /// </summary>
    [HttpGet("{materialId:guid}/attachments/{attachmentId:guid}/content")]
    public async Task<IActionResult> GetAttachmentContent(
        Guid materialId,
        Guid attachmentId,
        [FromQuery] string mode = "inline",
        CancellationToken cancellationToken = default)
    {
        var content = await _sender.Send(
            new GetMaterialAttachmentContentQuery(materialId, attachmentId),
            cancellationToken);

        if (content is null)
        {
            return NotFound();
        }

        if (string.Equals(mode, "download", StringComparison.OrdinalIgnoreCase))
        {
            return File(
                content.Stream,
                content.ContentType,
                fileDownloadName: content.FileName);
        }

        Response.Headers.ContentDisposition =
            $"inline; filename*=UTF-8''{Uri.EscapeDataString(content.FileName)}";

        return File(
            content.Stream,
            content.ContentType,
            enableRangeProcessing: true);
    }
}
