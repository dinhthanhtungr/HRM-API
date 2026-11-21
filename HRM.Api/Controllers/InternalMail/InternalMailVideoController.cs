using HRM.Application.Features.InternalMail.Queries.GetAttachmentContent;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.InternalMail;

[ApiController]
[Authorize]
[Route("api/v1/internal-mail")]
public sealed class InternalMailVideoController(ISender sender) : ControllerBase
{
    [HttpPost("attachments/{attachmentId:guid}/video/retry")]
    [HttpPost("conversations/{conversationId:guid}/related-attachments/{attachmentId:guid}/video/retry")]
    public async Task<IActionResult> Retry(Guid attachmentId, CancellationToken cancellationToken, Guid? conversationId = null)
    {
        var content = await sender.Send(new GetInternalMailAttachmentContentQuery
        {
            AttachmentId = attachmentId, ConversationId = conversationId, VideoVariant = "retry"
        }, cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        return content is null ? NotFound() : File(content.Stream, content.ContentType);
    }

    [HttpGet("attachments/{attachmentId:guid}/video/{variant}")]
    [HttpGet("conversations/{conversationId:guid}/related-attachments/{attachmentId:guid}/video/{variant}")]
    public async Task<IActionResult> Get(Guid attachmentId, string variant,
        CancellationToken cancellationToken, Guid? conversationId = null)
    {
        if (variant is not ("info" or "poster" or "playback")) return NotFound();
        try
        {
            var content = await sender.Send(new GetInternalMailAttachmentContentQuery
            {
                AttachmentId = attachmentId, ConversationId = conversationId, VideoVariant = variant
            }, cancellationToken);
            if (content is null) return NotFound();
            Response.Headers.CacheControl = "private, no-store";
            Response.Headers.XContentTypeOptions = "nosniff";
            return File(content.Stream, content.ContentType, enableRangeProcessing: variant != "info");
        }
        catch (FileNotFoundException) { return NotFound(); }
    }
}
