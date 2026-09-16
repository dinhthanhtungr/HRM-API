using HRM.Application.Features.NotificationHub.Dtos;
using MediatR;

namespace HRM.Application.Features.NotificationHub.Queries.GetItems;

/// <summary>
/// Lấy một trang notification đã enrich metadata conversation cho Notification Hub.
/// </summary>
public sealed class GetNotificationHubItemsQuery : IRequest<NotificationHubItemsDto>
{
    public string? CategoryCode { get; init; }
    public string? EventGroupCode { get; init; }
    /// <summary>
    /// Chỉ lấy notification thuộc conversation hiện còn ít nhất một tin nhắn gấp
    /// mà current employee nhận được. Không áp dụng cho notification không có conversation.
    /// </summary>
    public bool UrgentOnly { get; init; }
    public int Take { get; init; } = 20;
    public DateTime? AfterCreated { get; init; }
    public Guid? AfterId { get; init; }
}
