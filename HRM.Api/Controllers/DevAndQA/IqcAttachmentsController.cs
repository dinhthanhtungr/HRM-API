using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.DevAndQA.IQC.Services.Attachments;
using HRM.Domain.Enums.Attachment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.DevAndQA;

/// <summary>Upload/list/xem/download/xóa ảnh và tài liệu của QC theo dòng phiếu kho.</summary>
[ApiController]
[Authorize]
[Route("api/v1/dev-and-qa/iqc/{voucherDetailId:long}/attachments")]
public sealed class IqcAttachmentsController(IIqcAttachmentService attachments) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50_000_000)]
    public Task<IActionResult> Upload(long voucherDetailId, [FromForm] List<IFormFile> files,
        [FromQuery] AttachmentSlot slot = AttachmentSlot.QcReport, CancellationToken ct = default) =>
        ExecuteAsync(async () =>
        {
            if (files is null || files.Count == 0) return BadRequest(new { message = "Chưa chọn file." });
            var uploads = new List<AttachmentUploadFile>();
            try
            {
                foreach (var file in files)
                    uploads.Add(new AttachmentUploadFile(file.OpenReadStream(), file.FileName, file.ContentType, file.Length));
                return StatusCode(StatusCodes.Status201Created, await attachments.UploadAsync(voucherDetailId, slot, uploads, ct));
            }
            finally
            {
                foreach (var upload in uploads) await upload.Stream.DisposeAsync();
            }
        });

    [HttpGet]
    public Task<IActionResult> List(long voucherDetailId, [FromQuery] AttachmentSlot? slot, CancellationToken ct) =>
        ExecuteAsync(async () => Ok(await attachments.ListAsync(voucherDetailId, slot, ct)));

    [HttpGet("{attachmentId:guid}/content")]
    public Task<IActionResult> Content(long voucherDetailId, Guid attachmentId, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var content = await attachments.GetContentAsync(voucherDetailId, attachmentId, ct);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return content.ContentType is "image/jpeg" or "image/png" or "image/webp" or "image/gif" or "application/pdf"
                ? File(content.Stream, content.ContentType, enableRangeProcessing: true)
                : File(content.Stream, content.ContentType, content.FileName, enableRangeProcessing: true);
        });

    [HttpGet("{attachmentId:guid}/download")]
    public Task<IActionResult> Download(long voucherDetailId, Guid attachmentId, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            var content = await attachments.GetContentAsync(voucherDetailId, attachmentId, ct);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(content.Stream, content.ContentType, content.FileName, enableRangeProcessing: true);
        });

    [HttpDelete("{attachmentId:guid}")]
    public Task<IActionResult> Delete(long voucherDetailId, Guid attachmentId, CancellationToken ct) =>
        ExecuteAsync(async () =>
        {
            await attachments.DeleteAsync(voucherDetailId, attachmentId, ct);
            return NoContent();
        });

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (FileNotFoundException) { return NotFound(new { message = "Không tìm thấy file IQC." }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
