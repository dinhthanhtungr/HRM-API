using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.Attachments.Services;
using HRM.Domain.Enums.Attachment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Attachments;

[ApiController]
[Authorize]
[Route("api/collections/{collectionId:guid}/attachmentSchemas")]
public sealed class CollectionAttachmentsController : ControllerBase
{
    private readonly IAttachmentService _attachmentService;
    private readonly ICurrentUser _currentUser;

    public CollectionAttachmentsController(
        IAttachmentService attachmentService,
        ICurrentUser currentUser)
    {
        _attachmentService = attachmentService;
        _currentUser = currentUser;
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
        await _attachmentService.DeleteAsync(attachmentId, cancellationToken);

        return NoContent();
    }

    [HttpDelete("{attachmentId:guid}/hard")]
    public async Task<IActionResult> HardDelete(Guid attachmentId, CancellationToken cancellationToken)
    {
        await _attachmentService.HardDeleteAsync(attachmentId, cancellationToken);

        return NoContent();
    }

    private async Task<IActionResult> FileResultAsync(
        Guid attachmentId,
        string mode,
        CancellationToken cancellationToken)
    {
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
}
