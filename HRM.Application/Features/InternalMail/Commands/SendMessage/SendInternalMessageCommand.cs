using System.Text.Json.Serialization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.InternalMail.Dtos;
using MediatR;

namespace HRM.Application.Features.InternalMail.Commands.SendMessage;

/// <summary>
/// Gui message trong conversation da ton tai: participant hien tai hoac Developer co quyen gui trong company.
/// </summary>
public sealed class SendInternalMessageCommand : IRequest<OperationResult<SendInternalMessageResultDto>>
{
    public Guid ConversationId { get; set; }
    public string? AreaCode { get; set; }
    public string Body { get; set; } = string.Empty;
    public IReadOnlyList<Guid> MentionedEmployeeIds { get; set; } = Array.Empty<Guid>();
    public Guid? ReplyToMessageId { get; set; }
    public bool IsUrgent { get; set; }

    // Set only by the authorized forward handler; HTTP clients cannot bypass the normal body limit.
    [JsonIgnore]
    internal bool IsForwarded { get; init; }

    [JsonIgnore]
    internal IReadOnlyList<Guid> ForwardSourceConversationIds { get; init; } = Array.Empty<Guid>();

    [JsonIgnore]
    internal string? ForwardedSenderName { get; init; }

    [JsonIgnore]
    public IReadOnlyList<AttachmentUploadFile> Attachments { get; set; } = Array.Empty<AttachmentUploadFile>();
}
