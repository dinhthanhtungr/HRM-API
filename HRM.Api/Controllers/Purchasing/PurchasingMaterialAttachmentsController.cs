using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.Purchasing.Catalog.Services;
using HRM.Domain.Enums.Attachment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Purchasing;

[ApiController]
[Authorize]
[TypeFilter(typeof(PurchasingCatalogExceptionFilter))]
[Route("api/v1/purchasing/materials/{materialId:guid}/attachments")]
public sealed class PurchasingMaterialAttachmentsController(IMaterialAttachmentService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid materialId, CancellationToken ct)
    {
        try { return Ok(await service.ListAsync(materialId, ct)); }
        catch (FileNotFoundException) { return NotFound(); }
    }

    [HttpGet("{attachmentId:guid}/content")]
    public async Task<IActionResult> Content(Guid materialId, Guid attachmentId, [FromQuery] string mode = "inline", CancellationToken ct = default)
    {
        try
        {
            var file = await service.ContentAsync(materialId, attachmentId, ct);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            var inlineAllowed = file.ContentType is "application/pdf" or "image/jpeg" or "image/png" or "image/gif" or "image/webp" or "image/bmp";
            if (string.Equals(mode, "download", StringComparison.OrdinalIgnoreCase) || !inlineAllowed)
                return File(file.Stream, file.ContentType, file.FileName);
            Response.Headers.ContentDisposition = $"inline; filename*=UTF-8''{Uri.EscapeDataString(file.FileName)}";
            return File(file.Stream, file.ContentType, enableRangeProcessing: true);
        }
        catch (FileNotFoundException) { return NotFound(); }
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50_000_000)]
    public async Task<IActionResult> Upload(Guid materialId, [FromQuery] AttachmentSlot slot,
        [FromForm] List<IFormFile> files, [FromForm] DateTime? expectedUpdatedDate, CancellationToken ct)
    {
        var uploads = new List<AttachmentUploadFile>();
        try
        {
            foreach (var file in files)
                uploads.Add(new(file.OpenReadStream(), file.FileName, file.ContentType, file.Length));
            return StatusCode(StatusCodes.Status201Created,
                await service.UploadAsync(materialId, slot, uploads, expectedUpdatedDate, ct));
        }
        catch (FileNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        finally { foreach (var file in uploads) await file.Stream.DisposeAsync(); }
    }

    [HttpDelete("{attachmentId:guid}")]
    public async Task<IActionResult> Delete(Guid materialId, Guid attachmentId,
        [FromQuery] DateTime? expectedUpdatedDate, CancellationToken ct)
    {
        try { return Ok(await service.DeleteAsync(materialId, attachmentId, expectedUpdatedDate, ct)); }
        catch (FileNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
