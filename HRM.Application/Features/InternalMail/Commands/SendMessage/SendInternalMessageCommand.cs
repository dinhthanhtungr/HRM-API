using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.InternalMail.Commands.SendMessage;

/// <summary>
/// Gui them message trong conversation da ton tai. Nguoi gui phai la participant hien tai.
/// </summary>
public sealed class SendInternalMessageCommand : IRequest<OperationResult<SendInternalMessageResultDto>>
{
    public Guid ConversationId { get; set; }
    public string Body { get; set; } = string.Empty;
    public Guid? ReplyToMessageId { get; set; }
    public bool IsUrgent { get; set; }
}
