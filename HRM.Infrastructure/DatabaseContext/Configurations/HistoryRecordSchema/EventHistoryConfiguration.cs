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

    public class EventHistoryConfiguration : IEntityTypeConfiguration<EventHistory>
    {
        public void Configure(EntityTypeBuilder<EventHistory> entity)
        {
            entity.ToTable("EventHistory", schema: "historyrecords");
            entity.HasNoKey();
            entity.Property(x => x.Id)
                  .HasColumnName("eventhistory_id")
                  .UseIdentityAlwaysColumn();

            entity.Property(x => x.MachineId)
                  .HasColumnName("machine_id").HasColumnType("citext"); // giữ nguyên đúng tên cột trong sheet
                                                                        // Nếu cần so sánh không phân biệt hoa/thường:
                                                                        // entity.Property(x => x.MachineId).HasColumnType("citext");

            entity.Property(x => x.EventId)
                  .HasColumnName("event_id");

            entity.Property(x => x.CreatedAt)
                  .HasColumnName("created_at");

            // Index gợi ý cho truy vấn scada
            entity.HasIndex(x => x.CreatedAt)
                  .HasDatabaseName("ix_eventhistory_eventdate");
            entity.HasIndex(x => new { x.MachineId, x.CreatedAt })
                  .HasDatabaseName("ix_eventhistory_machine_eventdate");
        }
    }
}
