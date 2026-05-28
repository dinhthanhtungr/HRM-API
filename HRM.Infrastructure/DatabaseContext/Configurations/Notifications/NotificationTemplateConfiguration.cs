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
    public class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
    {
        private const string Schema = "notification";

        public void Configure(EntityTypeBuilder<NotificationTemplate> entity)
        {
            entity.ToTable("notification_templates", Schema);
            entity.HasKey(x => new { x.TemplateKey, x.Locale });

            entity.Property(x => x.TemplateKey).HasMaxLength(64).HasColumnName("template_key");
            entity.Property(x => x.Locale).HasMaxLength(10).HasColumnName("locale");
            entity.Property(x => x.TitleFormat).HasMaxLength(256).IsRequired().HasColumnName("title_format");
            entity.Property(x => x.MessageFormat).HasMaxLength(2000).IsRequired().HasColumnName("message_format");
        }
    }
}
