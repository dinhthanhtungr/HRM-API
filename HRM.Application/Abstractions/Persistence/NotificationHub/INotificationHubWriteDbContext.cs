using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HRM.Application.Abstractions.Persistence.NotificationHub;

/// <summary>
/// Bề mặt ghi tối thiểu cho thao tác đánh dấu toàn bộ Notification Hub đã đọc.
/// </summary>
public interface INotificationHubWriteDbContext
{
    DbSet<NotificationUserState> NotificationUserStates { get; }
    DbSet<InternalConversationParticipant> InternalConversationParticipants { get; }
    DbSet<InternalMessageReadState> InternalMessageReadStates { get; }

    Task<IDbContextTransaction> BeginNotificationHubTransactionAsync(
        CancellationToken cancellationToken = default);
}
