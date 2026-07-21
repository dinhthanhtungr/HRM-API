namespace HRM.Application.Features.Notifications.Dtos;

/// <summary>
/// Payload gọn lưu trong notification outbox để đẩy SignalR.
/// FE chỉ nhận NotificationId rồi gọi API có phân quyền để lấy detail/feed.
/// </summary>
public sealed class OutboxEnvelope
{
    public Guid CompanyId { get; set; }

    public Guid NotificationId { get; set; }

    public IReadOnlyCollection<Guid>? TargetUserIds { get; set; }

    public IReadOnlyCollection<string>? TargetRoles { get; set; }

    public IReadOnlyCollection<Guid>? TargetTeamIds { get; set; }
}
