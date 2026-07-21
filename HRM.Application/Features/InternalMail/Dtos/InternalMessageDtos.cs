using System.Text.Json.Serialization;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Application.Features.InternalMail.Dtos;

/// <summary>
/// Message tra ve cho thread. PayloadJson la metadata action/system; Body van la noi dung hien thi va search chinh.
/// </summary>
public sealed class InternalMessageDto
{
    public Guid MessageId { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderEmployeeId { get; set; }
    public string SenderName { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InternalMessageType MessageType { get; set; }

    public string Body { get; set; } = string.Empty;
    public string? PayloadJson { get; set; }
    public Guid? ReplyToMessageId { get; set; }
    public bool IsUrgent { get; set; }
    public DateTime SentAt { get; set; }
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public IReadOnlyList<InternalMessageReferenceDto> References { get; set; } = Array.Empty<InternalMessageReferenceDto>();
    public IReadOnlyList<InternalMessageAttachmentDto> Attachments { get; set; } = Array.Empty<InternalMessageAttachmentDto>();
}

public sealed class InternalMessageReferenceDto
{
    public Guid ReferenceId { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InternalMailRelatedType RelatedType { get; set; }

    public Guid RelatedId { get; set; }
    public string? RelatedExternalId { get; set; }
    public string? RelatedNameSnapshot { get; set; }
    public string? SnapshotJson { get; set; }
    public bool IsPrimary { get; set; }
}

public sealed class InternalMessageAttachmentDto
{
    public Guid AttachmentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}

public sealed class SendInternalMessageResultDto
{
    public Guid ConversationId { get; set; }
    public Guid MessageId { get; set; }
    public Guid NotificationId { get; set; }
}
