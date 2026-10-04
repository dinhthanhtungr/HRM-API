using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.InternalMail.Commands.SendMessage;

/// <summary>
/// Gui them message trong conversation da ton tai. Nguoi gui phai la participant hien tai.
/// </summary>
public sealed class SendInternalMessageCommand : IRequest<OperationResult<SendInternalMessageResultDto>>
{
    public Guid ConversationId { get; set; }
    public string? AreaCode { get; set; }
    public string Body { get; set; } = string.Empty;
    public Guid? ReplyToMessageId { get; set; }
    public bool IsUrgent { get; set; }

    // Set only by the authorized forward handler; HTTP clients cannot bypass the normal body limit.
    [JsonIgnore]
    internal bool IsForwarded { get; init; }

    [JsonIgnore]
    public IReadOnlyList<AttachmentUploadFile> Attachments { get; set; } = Array.Empty<AttachmentUploadFile>();
}
