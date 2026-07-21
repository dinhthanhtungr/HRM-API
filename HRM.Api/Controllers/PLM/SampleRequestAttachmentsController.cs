using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Attachments;
using HRM.Domain.Enums.Attachment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.PLM;

[ApiController]
[Authorize]
[Route("api/v1/plm/sample-requests/{sampleRequestId:guid}/attachments")]
public sealed class SampleRequestAttachmentsController : ControllerBase
{
    private readonly ISampleRequestAttachmentService _sampleRequestAttachmentService;
    private readonly ICurrentUser _currentUser;

    public SampleRequestAttachmentsController(
        ISampleRequestAttachmentService sampleRequestAttachmentService,
        ICurrentUser currentUser)
    {
        _sampleRequestAttachmentService = sampleRequestAttachmentService;
        _currentUser = currentUser;
    }

    [HttpPost]
    [RequestSizeLimit(50_000_000)]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = 50_000_000)]
    public async Task<IActionResult> UploadList(
        Guid sampleRequestId,
        [FromQuery] AttachmentSlot? slot,
        [FromForm] List<IFormFile> files,
        CancellationToken cancellationToken)
    {
        if (files is null || files.Count == 0)
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
            var createdBy = ResolveCurrentUserId();
            var result = await _sampleRequestAttachmentService.UploadListAsync(
                sampleRequestId,
                slot ?? AttachmentSlot.SampleRequest,
                uploadFiles,
                createdBy == Guid.Empty ? null : createdBy,
                cancellationToken);

            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
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
    public async Task<IActionResult> List(
        Guid sampleRequestId,
        [FromQuery] AttachmentSlot? slot,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sampleRequestAttachmentService.ListAsync(
                sampleRequestId,
                slot,
                cancellationToken);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("{attachmentId:guid}")]
    public async Task<IActionResult> Delete(
        Guid sampleRequestId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _sampleRequestAttachmentService.DeleteAsync(
                sampleRequestId,
                attachmentId,
                cancellationToken);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    private Guid ResolveCurrentUserId()
    {
        var userId = _currentUser.EmployeeId ?? _currentUser.UserId;
        return userId == Guid.Empty ? _currentUser.UserId : userId;
    }
}
