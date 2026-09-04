using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Abstractions.Persistence.Notifications;

/// <summary>
/// Bề mặt EF tối thiểu mà service notification cần dùng.
/// ApplicationDbContext implement interface này để tầng Application không phụ thuộc trực tiếp Infrastructure.
/// </summary>
public interface INotificationDbContext
{
    DbSet<Notification> Notifications { get; }

    DbSet<NotificationRecipient> NotificationRecipients { get; }

    DbSet<NotificationUserState> NotificationUserStates { get; }

    DbSet<OutboxMessage> OutboxMessages { get; }

    DbSet<WebPushSubscription> WebPushSubscriptions { get; }

    DbSet<InternalConversation> InternalConversations { get; }

    DbSet<InternalConversationParticipant> InternalConversationParticipants { get; }

    DbSet<Employee> Employees { get; }

    DbSet<ApplicationUser> Users { get; }

    DbSet<ApplicationRole> Roles { get; }

    DbSet<ApplicationUserRole> UserRoles { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
