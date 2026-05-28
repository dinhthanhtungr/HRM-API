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
    public class NotificationRecipientConfiguration : IEntityTypeConfiguration<NotificationRecipient>
    {
        private const string Schema = "notification";

        public void Configure(EntityTypeBuilder<NotificationRecipient> entity)
        {
            entity.ToTable("notification_recipients", Schema);
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.NotificationId).HasColumnName("notification_id");
            entity.Property(x => x.TargetUserId).HasColumnName("target_user_id");
            entity.Property(x => x.TargetRole).HasMaxLength(64).HasColumnName("target_role");
            entity.Property(x => x.TargetTeamId).HasColumnName("target_team_id");

            // FK -> Notification
            entity.HasOne(x => x.Notification)
                  .WithMany(n => n.Recipients)
                  .HasForeignKey(x => x.NotificationId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_notification_recipients_notification");

            // FK -> Employee (user)
            entity.HasOne(x => x.TargetUserNavigation)
                  .WithMany() // hoặc .WithMany(e => e.NotificationRecipientUsers)
                  .HasForeignKey(x => x.TargetUserId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_notification_recipients_user");

            // FK -> Team/Employee (team) — nếu Team là entity riêng, sửa kiểu navigation + constraint name
            entity.HasOne(x => x.TargetTeamNavigation)
                  .WithMany(x => x.NotificationRecipients)
                  .HasForeignKey(x => x.TargetTeamId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_notification_recipients_team");


            // Index
            entity.HasIndex(x => x.NotificationId).HasDatabaseName("ix_notification_recipients_notification");
            entity.HasIndex(x => x.TargetUserId).HasDatabaseName("ix_notification_recipients_target_user");
            entity.HasIndex(x => x.TargetRole).HasDatabaseName("ix_notification_recipients_target_role");
            entity.HasIndex(x => x.TargetTeamId).HasDatabaseName("ix_notification_recipients_target_team");
        }
    }
}
