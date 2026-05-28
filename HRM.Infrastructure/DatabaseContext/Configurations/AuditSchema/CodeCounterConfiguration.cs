using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.AuditSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.AuditSchema
{
    public class CodeCounterConfiguration : IEntityTypeConfiguration<CodeCounter>
    {
        public void Configure(EntityTypeBuilder<CodeCounter> entity)
        {
            entity.ToTable("code_counters", "Audit");

            // PK tổng hợp (Prefix, Ymd)
            entity.HasKey(x => new { x.Prefix, x.Ymd })
                  .HasName("pk_code_counters_prefix_ymd");

            entity.Property(x => x.Prefix)
                  .HasColumnName("prefix")
                  .HasColumnType("text")
                  .IsRequired();

            entity.Property(x => x.Ymd)
                  .HasColumnName("ymd")
                  .HasColumnType("integer")
                  .IsRequired();

            entity.Property(x => x.LastValue)
                  .HasColumnName("last_value")
                  .HasColumnType("integer")
                  .HasDefaultValue(0)
                  .IsRequired();

            // Index phụ để tra cứu theo prefix nhanh
            entity.HasIndex(x => x.Prefix)
                  .HasDatabaseName("ix_code_counters_prefix");

            // Nếu dự án dùng xmin:
            // entity.UseXminAsConcurrencyToken();
        }
    }
}
