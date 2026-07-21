using HRM.Application.Features.Attachments.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Attachments;

[ApiController]
[Authorize]
[Route("api/v1/attachments")]
public sealed class AttachmentsController : ControllerBase
{
    private readonly IAttachmentService _attachmentService;

    public AttachmentsController(IAttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
    }

    [HttpGet("{attachmentId:guid}")]
    public async Task<IActionResult> GetContent(
        Guid attachmentId,
        [FromQuery] string mode = "inline",
        CancellationToken cancellationToken = default)
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
