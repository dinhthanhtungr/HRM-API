using HRM.Application.Features.Attachments.Services;
using HRM.Application.Abstractions.Persistence.PLM;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Attachments;

[ApiController]
[Authorize]
[Route("api/v1/attachments")]
public sealed class AttachmentsController : ControllerBase
{
    private readonly IAttachmentService _attachmentService;
    private readonly IPLMReadDbContext _materials;

    public AttachmentsController(IAttachmentService attachmentService, IPLMReadDbContext materials)
    {
        _attachmentService = attachmentService;
        _materials = materials;
    }

    [HttpGet("{attachmentId:guid}")]
    public async Task<IActionResult> GetContent(
        Guid attachmentId,
        [FromQuery] string mode = "inline",
        CancellationToken cancellationToken = default)
    {
        if (await _materials.AttachmentModels.AsNoTracking().AnyAsync(x => x.AttachmentId == attachmentId &&
            _materials.Materials.Any(m => m.AttachmentCollectionId == x.AttachmentCollectionId), cancellationToken))
            return BadRequest(new { message = "Đọc tệp NVL qua endpoint của vật tư." });
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
