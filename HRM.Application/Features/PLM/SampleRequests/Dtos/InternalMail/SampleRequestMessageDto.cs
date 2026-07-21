using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Features.PLM.SampleRequests.Dtos.InternalMail;

/// <summary>
/// Mot dong trong lich su trao doi cua SampleRequest.
/// DTO nay phuc vu UI dang "mini email": form gui o tren, thread trao doi o duoi.
/// </summary>
public sealed class SampleRequestMessageDto
{
    public Guid ConversationId { get; set; }

    public Guid MessageId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public NotificationSeverity Severity { get; set; }

    public string Message { get; set; } = string.Empty;

    public bool IsUrgent { get; set; }

    public string MessageType { get; set; } = string.Empty;

    public Guid? ReplyToMessageId { get; set; }

    public Guid CreatedBy { get; set; }

    public string? CreatedByName { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadDate { get; set; }
}
