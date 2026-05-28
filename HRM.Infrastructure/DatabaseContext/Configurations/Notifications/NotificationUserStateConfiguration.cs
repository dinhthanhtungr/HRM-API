using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.Notifications;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs.Configurations.Notifications
{
    public class NotificationUserStateConfiguration : IEntityTypeConfiguration<NotificationUserState>
    {
        private const string Schema = "notification";

        public void Configure(EntityTypeBuilder<NotificationUserState> entity)
        {
            entity.ToTable("notification_user_states", Schema);
            entity.HasKey(x => new { x.NotificationId, x.UserId });

            entity.Property(x => x.NotificationId).HasColumnName("notification_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.IsRead).HasColumnName("is_read");
            entity.Property(x => x.ReadDate)
                  .HasPrecision(6)
                  .HasColumnName("read_date");
            entity.Property(x => x.IsArchived).HasColumnName("is_archived");

            entity.HasOne(x => x.Notification)
                  .WithMany(n => n.UserStates)
                  .HasForeignKey(x => x.NotificationId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_notification_user_states_notification");

            entity.HasOne(x => x.User)
                  .WithMany() // hoặc .WithMany(u => u.NotificationStates)
                  .HasForeignKey(x => x.UserId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_notification_user_states_user");

            entity.HasIndex(x => new { x.UserId, x.IsRead })
                  .HasDatabaseName("ix_notification_user_states_user_read");
            entity.HasIndex(x => x.NotificationId)
                  .HasDatabaseName("ix_notification_user_states_notification");

        }
    }
}
