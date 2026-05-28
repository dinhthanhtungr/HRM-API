using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.HistoryRecordSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.HistoryRecordSchema
{
    public class PlanGetbackHistoryConfiguration : IEntityTypeConfiguration<PlanGetbackHistory>
    {
        public void Configure(EntityTypeBuilder<PlanGetbackHistory> entity)
        {
            entity.ToTable("plan_getback_history", schema: "historyrecords");
            entity.HasNoKey();
            entity.Property(x => x.Id)
                  .HasColumnName("id")
                  .UseIdentityAlwaysColumn();

            entity.Property(x => x.ExternalId)
                  .HasColumnName("externalid")
                  .HasColumnType("citext");

            entity.Property(x => x.ColorCode)
                  .HasColumnName("colorcode")
                  .HasColumnType("citext");

            entity.Property(x => x.StatusBefore)
                  .HasColumnName("statusbefore")
                  .HasColumnType("citext");

            entity.Property(x => x.Reason)
                  .HasColumnName("reason"); // text

            entity.Property(x => x.PerformedBy)
                  .HasColumnName("performedby");

            entity.Property(x => x.CreatedAt)
                  .HasColumnName("created_at");


            // Index gợi ý cho truy vấn nhật ký
            entity.HasIndex(x => x.CreatedAt)
                  .HasDatabaseName("ix_plan_getback_history_created_at");
            entity.HasIndex(x => new { x.ExternalId, x.CreatedAt })
                  .HasDatabaseName("ix_plan_getback_history_externalid_created_at");
            entity.HasIndex(x => new { x.ColorCode, x.CreatedAt })
                  .HasDatabaseName("ix_plan_getback_history_colorcode_created_at");
        }
    }
}
