using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Features.Notifications.Dtos;

/// <summary>
/// Mô tả một yêu cầu phát notification từ nghiệp vụ.
/// Target có thể là nhân viên, role hoặc team; service sẽ tạo các dòng inbox theo EmployeeId.
/// </summary>
public sealed class PublishNotificationRequest
{
    public Guid? CompanyId { get; set; }

    public Guid? CreatedBy { get; set; }

    public string? CreatedByNameSnapshot { get; set; }

    public TopicNotifications Topic { get; set; }

    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Link { get; set; }

    public string? PayloadJson { get; set; }

    public IReadOnlyCollection<Guid>? TargetUserIds { get; set; }

    public IReadOnlyCollection<string>? TargetRoles { get; set; }

    public IReadOnlyCollection<Guid>? TargetTeamIds { get; set; }
}
