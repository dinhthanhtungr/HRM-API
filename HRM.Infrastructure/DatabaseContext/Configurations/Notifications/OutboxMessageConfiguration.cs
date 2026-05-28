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
    public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
    {
        private const string Schema = "notification";

        public void Configure(EntityTypeBuilder<OutboxMessage> entity)
        {
            entity.ToTable("outbox_messages", Schema);
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                  .ValueGeneratedOnAdd()
                  .HasColumnName("id");

            entity.Property(x => x.Type)
                  .HasMaxLength(128)
                  .IsRequired()
                  .HasColumnName("type");

            entity.Property(x => x.PayloadJson)
                  .IsRequired()
                  .HasColumnType("jsonb")
                  .HasColumnName("payload_json");

            entity.Property(x => x.CreatedAt)
                  .HasPrecision(6)
                  .HasColumnName("created_at");

            entity.Property(x => x.ProcessedAt)
                  .HasPrecision(6)
                  .HasColumnName("processed_at");

            entity.Property(x => x.Attempts)
                  .HasColumnName("attempts");

            entity.Property(x => x.Error)
                  .HasColumnName("error");

            // Index cho bản ghi chưa xử lý (Postgres dùng partial index)
            entity.HasIndex(e => e.ProcessedAt)
                  .HasDatabaseName("ix_outbox_unprocessed")
                  .HasFilter("processed_at IS NULL");
        }
    }
}

