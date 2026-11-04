using HRM.Domain.Enums.Orders;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Attachments.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using HRM.Application.Features.Purchasing.PurchaseOrders.Documents;


namespace HRM.Api.Controllers.Purchasing
{
    [ApiController]
    [TypeFilter(typeof(PurchaseOrderDocumentExceptionFilter))]
    [Route("api/v1/purchasing/purchase-orders/{purchaseOrderId:guid}/documents")]
    [Route("api/purchase-orders/{purchaseOrderId:guid}/documents")]
    [Authorize(Policy = PurchasingPolicies.ManagePurchaseOrders)]
    public class PurchaseOrderDocumentsController : Controller
    {
        private readonly IPurchaseOrderDocumentService _purchaseOrderDocumentService;

        public PurchaseOrderDocumentsController(IPurchaseOrderDocumentService purchaseOrderDocumentService)
        {
            _purchaseOrderDocumentService = purchaseOrderDocumentService;
        }

        [HttpGet]
        public async Task<IActionResult> GetList(
            Guid purchaseOrderId,
            [FromQuery] PurchaseOrderDocumentType? documentType,
            [FromQuery] bool includeInactive = false,
            CancellationToken ct = default)
        {
            var result = await _purchaseOrderDocumentService.GetListAsync(
                purchaseOrderId,
                documentType,
                includeInactive,
                ct);

            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("{documentId:guid}")]
        public async Task<IActionResult> GetById(
            Guid purchaseOrderId,
            Guid documentId,
            CancellationToken ct = default)
        {
            var result = await _purchaseOrderDocumentService.GetByIdAsync(purchaseOrderId, documentId, ct);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            Guid purchaseOrderId,
            [FromBody] CreatePurchaseOrderDocumentRequest request,
            CancellationToken ct = default)
        {
            var result = await _purchaseOrderDocumentService.CreateAsync(purchaseOrderId, request, ct);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPatch("{documentId:guid}")]
        public async Task<IActionResult> Update(
            Guid purchaseOrderId,
            Guid documentId,
            [FromBody] UpdatePurchaseOrderDocumentRequest request,
            CancellationToken ct = default)
        {
            var result = await _purchaseOrderDocumentService.UpdateAsync(purchaseOrderId, documentId, request, ct);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("{documentId:guid}")]
        public async Task<IActionResult> Delete(
            Guid purchaseOrderId,
            Guid documentId,
            CancellationToken ct = default)
        {
            var result = await _purchaseOrderDocumentService.DeleteAsync(purchaseOrderId, documentId, ct);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("{documentId:guid}/attachments")]
        public async Task<IActionResult> GetAttachments(
            Guid purchaseOrderId,
            Guid documentId,
            [FromQuery] PurchaseOrderDocumentType? slot,
            CancellationToken ct = default)
        {
            var result = await _purchaseOrderDocumentService.GetAttachmentsAsync(purchaseOrderId, documentId, slot, ct);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("{documentId:guid}/attachments")]
        [RequestSizeLimit(50_000_000)]
        [Consumes("multipart/form-data")]
        [RequestFormLimits(MultipartBodyLengthLimit = 50_000_000)]
        public async Task<IActionResult> UploadAttachments(
            Guid purchaseOrderId,
            Guid documentId,
            [FromForm] List<IFormFile> files,
            CancellationToken ct = default)
        {
            if (files is null || files.Count == 0) return BadRequest(new { message = "Chưa chọn file." });
            var uploads = new List<AttachmentUploadFile>();
            try
            {
                foreach (var file in files)
                    uploads.Add(new AttachmentUploadFile(file.OpenReadStream(), file.FileName, file.ContentType, file.Length));
                var result = await _purchaseOrderDocumentService.UploadAttachmentsAsync(
                    purchaseOrderId,
                    documentId,
                    uploads,
                    ct);

                return result.Success
                    ? StatusCode(StatusCodes.Status201Created, result)
                    : BadRequest(result);
            }
            finally { foreach (var upload in uploads) await upload.Stream.DisposeAsync(); }
        }

        [HttpPost("attachments")]
        [RequestSizeLimit(50_000_000)]
        [Consumes("multipart/form-data")]
        [RequestFormLimits(MultipartBodyLengthLimit = 50_000_000)]
        public async Task<IActionResult> UploadAttachmentsByType(
            Guid purchaseOrderId,
            [FromQuery] PurchaseOrderDocumentType documentType,
            [FromForm] List<IFormFile> files,
            CancellationToken ct = default)
        {
            if (files is null || files.Count == 0) return BadRequest(new { message = "Chưa chọn file." });
            var uploads = new List<AttachmentUploadFile>();
            try
            {
                foreach (var file in files)
                    uploads.Add(new AttachmentUploadFile(file.OpenReadStream(), file.FileName, file.ContentType, file.Length));
                var result = await _purchaseOrderDocumentService.UploadAttachmentsByTypeAsync(
                    purchaseOrderId,
                    documentType,
                    uploads,
                    ct);

                return result.Success
                    ? StatusCode(StatusCodes.Status201Created, result)
                    : BadRequest(result);
            }
            finally { foreach (var upload in uploads) await upload.Stream.DisposeAsync(); }
        }

        [HttpGet("{documentId:guid}/attachments/{attachmentId:guid}/content")]
        public async Task<IActionResult> GetAttachmentContent(
            Guid purchaseOrderId,
            Guid documentId,
            Guid attachmentId,
            [FromQuery] string mode = "inline",
            [FromQuery] string? size = null,
            CancellationToken ct = default)
        {
            var result = await _purchaseOrderDocumentService.GetAttachmentContentAsync(
                purchaseOrderId,
                documentId,
                attachmentId,
                ct);

            if (!result.Success || result.Data is null)
                return BadRequest(result);

            var sr = result.Data;
            var isDownload = string.Equals(mode, "download", StringComparison.OrdinalIgnoreCase);
            var isThumb = string.Equals(size, "thumb", StringComparison.OrdinalIgnoreCase);

            if (isDownload)
                return File(sr.Stream, sr.ContentType, fileDownloadName: sr.FileName);

            if (isThumb && sr.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                await using var sourceStream = sr.Stream;
                using var image = await Image.LoadAsync(sr.Stream, ct);

                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(320, 320)
                }));

                var output = new MemoryStream();
                await image.SaveAsJpegAsync(output, new JpegEncoder
                {
                    Quality = 70
                }, ct);

                output.Position = 0;
                Response.Headers["Content-Disposition"] =
                    $"inline; filename*=UTF-8''{Uri.EscapeDataString(sr.FileName)}";
                return File(output, "image/jpeg");
            }

            Response.Headers["Content-Disposition"] =
                $"inline; filename*=UTF-8''{Uri.EscapeDataString(sr.FileName)}";
            return File(sr.Stream, sr.ContentType);
        }

        [HttpPatch("{documentId:guid}/attachments/{attachmentId:guid}")]
        public async Task<IActionResult> DeleteAttachment(
            Guid purchaseOrderId,
            Guid documentId,
            Guid attachmentId,
            CancellationToken ct = default)
        {
            var result = await _purchaseOrderDocumentService.DeleteAttachmentAsync(
                purchaseOrderId,
                documentId,
                attachmentId,
                ct);

            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("{documentId:guid}/attachments/{attachmentId:guid}/hard")]
        public async Task<IActionResult> HardDeleteAttachment(
            Guid purchaseOrderId,
            Guid documentId,
            Guid attachmentId,
            CancellationToken ct = default)
        {
            var result = await _purchaseOrderDocumentService.HardDeleteAttachmentAsync(
                purchaseOrderId,
                documentId,
                attachmentId,
                ct);

            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}
