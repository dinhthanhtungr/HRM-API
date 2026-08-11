using HRM.Application.Features.PLM.Materials.Queries.GetMaterialAttachmentContent;
using HRM.Application.Features.PLM.Materials.Queries.GetMaterialPreview;
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
