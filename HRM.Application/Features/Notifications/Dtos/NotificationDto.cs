using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Features.Notifications.Dtos;

/// <summary>
/// Notification Hub item for the current employee.
/// Presentation codes are derived from Topic and are never stored as notification columns.
/// </summary>
public sealed class NotificationDto
{
    public Guid Id { get; set; }
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
    public Guid? MessageId { get; internal set; }
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
