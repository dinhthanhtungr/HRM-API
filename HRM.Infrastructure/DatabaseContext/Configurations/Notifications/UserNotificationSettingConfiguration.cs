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
    public class UserNotificationSettingConfiguration : IEntityTypeConfiguration<UserNotificationSetting>
    {
        private const string Schema = "notification";

        public void Configure(EntityTypeBuilder<UserNotificationSetting> entity)
        {
            entity.ToTable("user_notification_settings", Schema);
            entity.HasKey(x => x.UserId);

            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Channels).HasMaxLength(128).IsRequired().HasColumnName("channels");
            entity.Property(x => x.MinSeverity).HasConversion<int>().IsRequired().HasColumnName("min_severity");

            // Npgsql: TimeSpan thường map 'interval'
            entity.Property(x => x.QuietFrom).HasColumnType("interval").HasColumnName("quiet_from");
            entity.Property(x => x.QuietTo).HasColumnType("interval").HasColumnName("quiet_to");

            entity.Property(x => x.Locale).HasMaxLength(10).HasColumnName("locale");

            entity.HasOne(x => x.User)
                  .WithMany() // hoặc .WithOne(u => u.NotificationSetting)
                  .HasForeignKey(x => x.UserId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_user_notification_settings_user");
        }
    }
}
