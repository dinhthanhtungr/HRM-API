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
    public class AssignTaskConfiguration : IEntityTypeConfiguration<AssignTask>
    {
        public void Configure(EntityTypeBuilder<AssignTask> entity)
        {
            entity.ToTable("assign_task", "historyrecords");
            entity.HasNoKey();

            entity.Property(x => x.AssignId)
                  .HasColumnName("assign_id")
                  .UseIdentityAlwaysColumn();

            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.ShiftId).HasColumnName("shiftid");
            entity.Property(x => x.MachineId).HasColumnName("machineid").HasColumnType("citext");
            entity.Property(x => x.OperatorId).HasColumnName("operator");
            entity.Property(x => x.Note).HasColumnName("note");

            entity.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_assign_created");
            entity.HasIndex(x => new { x.MachineId, x.CreatedAt }).HasDatabaseName("ix_assign_machine_created");
            entity.HasIndex(x => x.ShiftId).HasDatabaseName("ix_assign_shift");
        }
    }
}
