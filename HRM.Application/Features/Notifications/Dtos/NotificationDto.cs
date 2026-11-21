using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Features.Notifications.Dtos;

/// <summary>
/// Notification Hub item for the current employee.
/// Presentation codes are derived from Topic and are never stored as notification columns.
/// </summary>
public sealed class NotificationDto
{
    public IReadOnlyList<NotificationSalesOrderInfoDto> SalesOrderInfo { get; internal set; } = [];
    public Guid Id { get; set; }
    public string? StreamKey { get; internal set; }
    public int? StreamUnreadCount { get; internal set; }
    /// <summary>Authoritative visible-message unread count for quotation conversations, including zero.</summary>
    public int? ConversationUnreadCount { get; internal set; }
    public string? AreaCode { get; set; }
    public TopicNotifications Topic { get; set; }
    public string TopicCode => NotificationTopicCodes.GetCode(Topic);
    public string CategoryCode => NotificationTopicCategoryRules.GetCategoryCode(Topic, CreatedDate);
    public string EventGroupCode => NotificationTopicCategoryRules.GetEventGroupCode(Topic, CreatedDate);
    public NotificationSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Link { get; set; }
    public string? PayloadJson { get; set; }
    public NotificationContextDto? Context { get; internal set; }
    public Guid? ConversationId { get; internal set; }
    /// <summary>
    /// Tiêu đề conversation hiện tại mà employee đang được phép xem. FE dùng field này làm title canonical khi gom feed theo thread.
    /// </summary>
    public string? ConversationTitle { get; internal set; }
    /// <summary>Current display metadata, resolved only for a readable same-company conversation in this feed page.</summary>
    public HRM.Application.Features.InternalMail.Dtos.SampleRequestConversationInfoDto? SampleRequestInfo { get; internal set; }
    public HRM.Application.Features.InternalMail.Dtos.QuotationConversationInfoDto? QuotationInfo { get; internal set; }
    public Guid? MessageId { get; internal set; }
    public Guid? MentionedMessageId { get; internal set; }
    public int UnreadMentionCount { get; internal set; }
    public string? PersonalReason { get; internal set; }
    public DateTime? PersonalActivityDate { get; internal set; }
    public Guid? PersonalMessageId { get; internal set; }
    public DateTime CreatedDate { get; set; }
    public Guid CompanyId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadDate { get; set; }
    public Guid? CreatedBy { get; set; }
    public string? CreatedByNameSnapshot { get; set; }
}

public sealed class NotificationContextDto
{
    public string AggregateType { get; set; } = string.Empty;
    public Guid? AggregateId { get; set; }
    public string? AggregateCode { get; set; }
}

public sealed record NotificationSalesOrderInfoDto(Guid Id, string ExternalId, string CustomerCode, string CustomerName, IReadOnlyList<string> ProductCodes);
