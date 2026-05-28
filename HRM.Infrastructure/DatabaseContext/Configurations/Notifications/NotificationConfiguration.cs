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
    public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        private const string Schema = "notification";

        public void Configure(EntityTypeBuilder<Notification> entity)
        {
            entity.ToTable("notifications", Schema);
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Topic).HasConversion<int>().IsRequired().HasColumnName("topic");
            entity.Property(x => x.Severity).HasConversion<int>().IsRequired().HasColumnName("severity");
            entity.Property(x => x.Title).HasMaxLength(256).IsRequired().HasColumnName("title");
            entity.Property(x => x.Message).HasMaxLength(2000).IsRequired().HasColumnName("message");
            entity.Property(x => x.Link).HasMaxLength(512).HasColumnName("link");
            entity.Property(x => x.PayloadJson).HasColumnType("jsonb").HasColumnName("payload_json");
            entity.Property(x => x.CreatedDate).HasPrecision(6).HasColumnName("created_date");

            entity.Property(x => x.CompanyId).IsRequired().HasColumnName("company_id");

            entity.HasOne(x => x.Company)
                  .WithMany()
                  .HasForeignKey(x => x.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_notifications_company");

            entity.HasOne(x => x.CreatedByEmployeeNavigation)
                  .WithMany(x => x.CreatedByEmployeeNavigations)
                  .HasForeignKey(x => x.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_notifications_created_by_employee");

            entity.HasIndex(x => new { x.CompanyId, x.Topic, x.CreatedDate })
                  .HasDatabaseName("ix_notifications_company_topic_created");
        }
    }
}
