using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Application.Abstractions.Persistence.Notifications;
using HRM.Domain.Entities.Notifications;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs
{
    public partial class ApplicationDbContext : INotificationDbContext
    {
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<NotificationRecipient> NotificationRecipients => Set<NotificationRecipient>();
        public DbSet<NotificationUserState> NotificationUserStates => Set<NotificationUserState>();
        public DbSet<UserNotificationSetting> UserNotificationSettings => Set<UserNotificationSetting>();
        public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
        public DbSet<WebPushSubscription> WebPushSubscriptions => Set<WebPushSubscription>();
    }
}
