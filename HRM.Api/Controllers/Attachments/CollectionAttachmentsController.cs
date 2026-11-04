using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.Attachments.Services;
using HRM.Domain.Enums.Attachment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Persistence.PLM;
using Microsoft.EntityFrameworkCore;

namespace HRM.Api.Controllers.Attachments;

[ApiController]
[Authorize]
[Route("api/collections/{collectionId:guid}/attachmentSchemas")]
public sealed class CollectionAttachmentsController : ControllerBase
{
    private readonly IAttachmentService _attachmentService;
    private readonly ICurrentUser _currentUser;
    private readonly IPurchaseOrderDbContext _purchaseOrders;
    private readonly IPLMReadDbContext _materials;

    public CollectionAttachmentsController(
        IAttachmentService attachmentService,
        ICurrentUser currentUser,
        IPurchaseOrderDbContext purchaseOrders, IPLMReadDbContext materials)
    {
        _attachmentService = attachmentService;
        _currentUser = currentUser;
        _purchaseOrders = purchaseOrders;
        _materials = materials;
    }

    [HttpPost]
    [RequestSizeLimit(50_000_000)]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = 50_000_000)]
    public async Task<IActionResult> UploadList(
        Guid collectionId,
        [FromQuery] AttachmentSlot slot,
        [FromForm] List<IFormFile> files,
        CancellationToken cancellationToken)
    {
        if (await IsMaterialCollectionAsync(collectionId, cancellationToken)) return BadRequest(new { message = "Upload tệp NVL qua endpoint của vật tư." });
        if (await IsPurchaseOrderCollectionAsync(collectionId, cancellationToken)) return BadRequest(new { message = "Thao tác chứng từ mua hàng qua endpoint của đơn mua hàng." });
        if (files.Count == 0)
        {
            return BadRequest("No files provided.");
        }

        var uploadFiles = new List<AttachmentUploadFile>(files.Count);

        foreach (var file in files)
        {
            uploadFiles.Add(new AttachmentUploadFile(
                file.OpenReadStream(),
                file.FileName,
                file.ContentType,
                file.Length));
        }

        try
        {
            var createdBy = _currentUser.EmployeeId ?? _currentUser.UserId;
            var result = await _attachmentService.UploadListAsync(
                collectionId,
                slot,
                uploadFiles,
                createdBy == Guid.Empty ? null : createdBy,
                cancellationToken);

            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        finally
        {
            foreach (var uploadFile in uploadFiles)
            {
                await uploadFile.Stream.DisposeAsync();
            }
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AttachmentDto>>> List(
        Guid collectionId,
        [FromQuery] AttachmentSlot? slot,
        CancellationToken cancellationToken)
    {
        if (await IsMaterialCollectionAsync(collectionId, cancellationToken)) return BadRequest(new { message = "Đọc tệp NVL qua endpoint của vật tư." });
        if (await IsPurchaseOrderCollectionAsync(collectionId, cancellationToken)) return BadRequest(new { message = "Đọc chứng từ mua hàng qua endpoint của đơn mua hàng." });
        var result = await _attachmentService.ListAsync(collectionId, slot, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{attachmentId:guid}/content")]
    public async Task<IActionResult> GetContent(
        Guid attachmentId,
        [FromQuery] string mode = "inline",
        CancellationToken cancellationToken = default)
    {
        return await FileResultAsync(attachmentId, mode, cancellationToken);
    }

    [HttpPatch("{attachmentId:guid}")]
    public async Task<IActionResult> Delete(Guid attachmentId, CancellationToken cancellationToken)
    {
        if (await IsMaterialAttachmentAsync(attachmentId, cancellationToken)) return BadRequest(new { message = "Xóa tệp NVL qua endpoint của vật tư." });
        if (await IsPurchaseOrderAttachmentAsync(attachmentId, cancellationToken)) return BadRequest(new { message = "Xóa chứng từ mua hàng qua endpoint của đơn mua hàng." });
        await _attachmentService.DeleteAsync(attachmentId, cancellationToken);

        return NoContent();
    }

    [HttpDelete("{attachmentId:guid}/hard")]
    public async Task<IActionResult> HardDelete(Guid attachmentId, CancellationToken cancellationToken)
    {
        if (await IsMaterialAttachmentAsync(attachmentId, cancellationToken)) return BadRequest(new { message = "Xóa tệp NVL qua endpoint của vật tư." });
        if (await IsPurchaseOrderAttachmentAsync(attachmentId, cancellationToken)) return BadRequest(new { message = "Xóa chứng từ mua hàng qua endpoint của đơn mua hàng." });
        await _attachmentService.HardDeleteAsync(attachmentId, cancellationToken);

        return NoContent();
    }

    private async Task<IActionResult> FileResultAsync(
        Guid attachmentId,
        string mode,
        CancellationToken cancellationToken)
    {
        if (await IsMaterialAttachmentAsync(attachmentId, cancellationToken)) return BadRequest(new { message = "Đọc tệp NVL qua endpoint của vật tư." });
        if (await IsPurchaseOrderAttachmentAsync(attachmentId, cancellationToken)) return BadRequest(new { message = "Đọc chứng từ mua hàng qua endpoint của đơn mua hàng." });
        try
        {
            var content = await _attachmentService.GetContentAsync(attachmentId, cancellationToken);
            var isDownload = string.Equals(mode, "download", StringComparison.OrdinalIgnoreCase);

            if (isDownload)
            {
                return File(content.Stream, content.ContentType, fileDownloadName: content.FileName);
            }

            Response.Headers.ContentDisposition =
                $"inline; filename*=UTF-8''{Uri.EscapeDataString(content.FileName)}";

            return File(content.Stream, content.ContentType, enableRangeProcessing: true);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    private Task<bool> IsMaterialCollectionAsync(Guid id, CancellationToken ct) =>
        _materials.Materials.AsNoTracking().AnyAsync(x => x.AttachmentCollectionId == id, ct);

    private Task<bool> IsMaterialAttachmentAsync(Guid id, CancellationToken ct) =>
        _materials.AttachmentModels.AsNoTracking().AnyAsync(x => x.AttachmentId == id &&
            _materials.Materials.Any(m => m.AttachmentCollectionId == x.AttachmentCollectionId), ct);

    private Task<bool> IsPurchaseOrderCollectionAsync(Guid id, CancellationToken ct) =>
        _purchaseOrders.PurchaseOrderDocuments.AsNoTracking().AnyAsync(x => x.AttachmentCollectionId == id, ct);

    private Task<bool> IsPurchaseOrderAttachmentAsync(Guid id, CancellationToken ct) =>
        _purchaseOrders.AttachmentModels.AsNoTracking().AnyAsync(file => file.AttachmentId == id &&
            _purchaseOrders.PurchaseOrderDocuments.Any(doc => doc.AttachmentCollectionId == file.AttachmentCollectionId), ct);
}
