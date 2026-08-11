using HRM.Application.Features.Notifications.Dtos;
using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Features.Notifications.Services;

/// <summary>
/// Cổng nghiệp vụ notification dùng cho handler và API notification.
/// Các dòng NotificationUserState là nguồn dữ liệu chính cho feed và số chưa đọc.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Tạo notification, dòng audit người nhận, trạng thái inbox của user và outbox realtime.
    /// </summary>
    Task<Guid> PublishAsync(PublishNotificationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy feed notification của nhân viên hiện tại bằng keyset paging.
    /// </summary>
    Task<IReadOnlyList<NotificationDto>> GetFeedAsync(
        int take = 20,
        Guid? afterId = null,
        DateTime? afterCreated = null,
        string? categoryCode = null,
        string? eventGroupCode = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Đếm notification chưa đọc mà nhân viên hiện tại được thấy.
    /// </summary>
    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Đếm notification chưa đọc theo từng category và trả tổng cho nhân viên hiện tại.
    /// </summary>
    Task<NotificationUnreadSummaryDto> GetUnreadSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Chỉ trả notification khi nhân viên hiện tại có UserState tương ứng.
    /// </summary>
    Task<NotificationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đánh dấu một notification là đã đọc cho nhân viên hiện tại.
    /// </summary>
    Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đánh dấu toàn bộ notification chưa đọc là đã đọc cho nhân viên hiện tại.
    /// </summary>
    Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Archive notification trong inbox cua chinh nhan vien hien tai.
    /// </summary>
    Task<bool> ArchiveCurrentAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Thu hoi notification khoi inbox cua mot nhan vien bang cach archive UserState, khong xoa du lieu that.
    /// </summary>
    Task<bool> ArchiveRecipientAsync(Guid id, Guid employeeId, CancellationToken cancellationToken = default);
}
