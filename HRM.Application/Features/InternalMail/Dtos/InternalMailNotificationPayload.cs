using System.Text.Json.Serialization;

namespace HRM.Application.Features.InternalMail.Dtos;

/// <summary>
/// Payload nho de notification tro ve dung conversation/message. SignalR van chi day notificationId.
/// </summary>
public sealed class InternalMailNotificationPayload
{
    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = "InternalMailMessage";

    [JsonPropertyName("conversationId")]
    public Guid ConversationId { get; set; }

    [JsonPropertyName("messageId")]
    public Guid MessageId { get; set; }

    [JsonPropertyName("relatedType")]
    public string? RelatedType { get; set; }

    [JsonPropertyName("relatedId")]
    public Guid? RelatedId { get; set; }

    [JsonPropertyName("isUrgent")]
    public bool IsUrgent { get; set; }

    [JsonPropertyName("attachmentCount")]
    public int AttachmentCount { get; set; }
}
