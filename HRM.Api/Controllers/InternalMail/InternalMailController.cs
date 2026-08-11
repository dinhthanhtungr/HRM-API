using System.Text.Json;
using HRM.Application.Features.InternalMail.Commands.AddParticipants;
using HRM.Application.Features.InternalMail.Commands.CreateConversation;
using HRM.Application.Features.InternalMail.Commands.DeleteMessage;
using HRM.Application.Features.InternalMail.Commands.MarkConversationRead;
using HRM.Application.Features.InternalMail.Commands.RemoveParticipant;
using HRM.Application.Features.InternalMail.Commands.SendMessage;
using HRM.Application.Features.InternalMail.Commands.UpdateConversationPreference;
using HRM.Application.Features.InternalMail.Commands.UpdateMessage;
using HRM.Application.Features.InternalMail.Queries.GetConversationDetail;
using HRM.Application.Features.InternalMail.Queries.GetConversationAttachments;
using HRM.Application.Features.InternalMail.Queries.GetAttachmentContent;
using HRM.Application.Features.InternalMail.Queries.GetConversations;
using HRM.Application.Features.InternalMail.Queries.GetMessageContext;
using HRM.Application.Features.InternalMail.Queries.GetMessages;
using HRM.Application.Features.InternalMail.Queries.SearchMessages;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.InternalMail;

/// <summary>
/// API hom thu noi bo. Moi endpoint deu yeu cau current employee la participant va conversation cung company.
/// Notification chi duoc tao nhu side effect khi gui message, khong phai nguon du lieu cua thread.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/internal-mail")]
public sealed class InternalMailController : ControllerBase
{
    private readonly ISender _sender;

    public InternalMailController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations(
        [FromQuery] GetInternalConversationsQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(query, cancellationToken));
    }

    [HttpPost("conversations")]
    public async Task<IActionResult> CreateConversation(
        [FromBody] CreateInternalConversationCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(command, cancellationToken);
        if (!result.Success || result.Data is null)
        {
            return BadRequest(result);
        }

        return CreatedAtAction(
            nameof(GetConversationDetail),
            new { conversationId = result.Data.ConversationId },
            result);
    }

    [HttpGet("conversations/{conversationId:guid}")]
    public async Task<IActionResult> GetConversationDetail(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetInternalConversationDetailQuery
        {
            ConversationId = conversationId
        }, cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid conversationId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetInternalMessagesQuery
        {
            ConversationId = conversationId,
            PageNumber = pageNumber,
            PageSize = pageSize
        }, cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("conversations/{conversationId:guid}/attachments")]
    public async Task<IActionResult> GetConversationAttachments(
        Guid conversationId,
        [FromQuery] GetInternalConversationAttachmentsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetInternalConversationAttachmentsQuery
        {
            ConversationId = conversationId,
            Kind = query.Kind,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        }, cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("conversations/{conversationId:guid}/messages/search")]
    public async Task<IActionResult> SearchMessages(
        Guid conversationId,
        [FromQuery] SearchInternalMessagesQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SearchInternalMessagesQuery
        {
            ConversationId = conversationId,
            Q = query.Q,
            Search = query.Search,
            Keyword = query.Keyword,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        }, cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("conversations/{conversationId:guid}/messages/{messageId:guid}/context")]
    public async Task<IActionResult> GetMessageContext(
        Guid conversationId,
        Guid messageId,
        [FromQuery] int before = 10,
        [FromQuery] int after = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetInternalMessageContextQuery
        {
            ConversationId = conversationId,
            MessageId = messageId,
            Before = before,
            After = after
        }, cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("conversations/{conversationId:guid}/messages")]
    [Consumes("application/json", "multipart/form-data")]
    [RequestSizeLimit(50_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50_000_000)]
    public async Task<IActionResult> SendMessage(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        if (!Request.HasFormContentType)
        {
            if (!Request.HasJsonContentType())
            {
                return StatusCode(StatusCodes.Status415UnsupportedMediaType);
            }

            var command = await JsonSerializer.DeserializeAsync<SendInternalMessageCommand>(
                Request.Body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                cancellationToken);
            if (command is null)
            {
                return BadRequest("A message payload is required.");
            }

            command.ConversationId = conversationId;
            var result = await _sender.Send(command, cancellationToken);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        var form = await Request.ReadFormAsync(cancellationToken);
        var replyToMessageIdText = form["replyToMessageId"].FirstOrDefault();
        if (!Guid.TryParse(replyToMessageIdText, out var replyToMessageId) &&
            !string.IsNullOrWhiteSpace(replyToMessageIdText))
        {
            return BadRequest("ReplyToMessageId is invalid.");
        }

        var isUrgent = bool.TryParse(form["isUrgent"].FirstOrDefault(), out var parsedIsUrgent) && parsedIsUrgent;
        var uploadFiles = form.Files
            .Select(file => new HRM.Application.Features.Attachments.Dtos.AttachmentUploadFile(
                file.OpenReadStream(),
                file.FileName,
                file.ContentType,
                file.Length))
            .ToList();

        try
        {
            var result = await _sender.Send(new SendInternalMessageCommand
            {
                ConversationId = conversationId,
                Body = form["body"].FirstOrDefault() ?? string.Empty,
                ReplyToMessageId = replyToMessageId == Guid.Empty ? null : replyToMessageId,
                IsUrgent = isUrgent,
                Attachments = uploadFiles
            }, cancellationToken);

            return result.Success ? Ok(result) : BadRequest(result);
        }
        finally
        {
            foreach (var uploadFile in uploadFiles)
            {
                await uploadFile.Stream.DisposeAsync();
            }
        }
    }

    [HttpGet("attachments/{attachmentId:guid}")]
    public async Task<IActionResult> GetAttachmentContent(
        Guid attachmentId,
        [FromQuery] string mode = "inline",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var content = await _sender.Send(new GetInternalMailAttachmentContentQuery
            {
                AttachmentId = attachmentId
            }, cancellationToken);
            if (content is null)
            {
                return NotFound();
            }

            if (string.Equals(mode, "download", StringComparison.OrdinalIgnoreCase))
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

    [HttpGet("attachments/{attachmentId:guid}/thumbnail")]
    public async Task<IActionResult> GetAttachmentThumbnail(
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var content = await _sender.Send(new GetInternalMailAttachmentContentQuery
            {
                AttachmentId = attachmentId,
                Thumbnail = true
            }, cancellationToken);

            if (content is null)
            {
                return NotFound();
            }

            Response.Headers.CacheControl = "private, max-age=86400";
            return File(content.Stream, content.ContentType, enableRangeProcessing: true);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPatch("messages/{messageId:guid}")]
    public async Task<IActionResult> UpdateMessage(
        Guid messageId,
        [FromBody] UpdateInternalMessageCommand command,
        CancellationToken cancellationToken)
    {
        command.MessageId = messageId;
        var result = await _sender.Send(command, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("messages/{messageId:guid}")]
    public async Task<IActionResult> DeleteMessage(Guid messageId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteInternalMessageCommand
        {
            MessageId = messageId
        }, cancellationToken);

        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpPost("conversations/{conversationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid conversationId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new MarkInternalConversationReadCommand
        {
            ConversationId = conversationId
        }, cancellationToken);

        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpPatch("conversations/{conversationId:guid}/preferences")]
    public async Task<IActionResult> UpdatePreferences(
        Guid conversationId,
        [FromBody] UpdateInternalConversationPreferenceCommand command,
        CancellationToken cancellationToken)
    {
        command.ConversationId = conversationId;
        var result = await _sender.Send(command, cancellationToken);
        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpGet("conversations/{conversationId:guid}/participants")]
    public async Task<IActionResult> GetParticipants(Guid conversationId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetInternalConversationDetailQuery
        {
            ConversationId = conversationId
        }, cancellationToken);

        return result is null ? NotFound() : Ok(result.Participants);
    }

    [HttpPost("conversations/{conversationId:guid}/participants")]
    public async Task<IActionResult> AddParticipants(
        Guid conversationId,
        [FromBody] AddInternalConversationParticipantsCommand command,
        CancellationToken cancellationToken)
    {
        command.ConversationId = conversationId;
        var result = await _sender.Send(command, cancellationToken);
        return result.Success ? NoContent() : BadRequest(result);
    }

    [HttpDelete("conversations/{conversationId:guid}/participants/{employeeId:guid}")]
    public async Task<IActionResult> RemoveParticipant(
        Guid conversationId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new RemoveInternalConversationParticipantCommand
        {
            ConversationId = conversationId,
            EmployeeId = employeeId
        }, cancellationToken);

        return result.Success ? NoContent() : BadRequest(result);
    }
}
