using HRM.Application.Features.InternalMail.Commands.AddParticipants;
using HRM.Application.Features.InternalMail.Commands.CreateConversation;
using HRM.Application.Features.InternalMail.Commands.DeleteMessage;
using HRM.Application.Features.InternalMail.Commands.MarkConversationRead;
using HRM.Application.Features.InternalMail.Commands.RemoveParticipant;
using HRM.Application.Features.InternalMail.Commands.SendMessage;
using HRM.Application.Features.InternalMail.Commands.UpdateConversationPreference;
using HRM.Application.Features.InternalMail.Commands.UpdateMessage;
using HRM.Application.Features.InternalMail.Queries.GetConversationDetail;
using HRM.Application.Features.InternalMail.Queries.GetConversations;
using HRM.Application.Features.InternalMail.Queries.GetMessages;
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

    [HttpPost("conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> SendMessage(
        Guid conversationId,
        [FromBody] SendInternalMessageCommand command,
        CancellationToken cancellationToken)
    {
        command.ConversationId = conversationId;
        var result = await _sender.Send(command, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
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
