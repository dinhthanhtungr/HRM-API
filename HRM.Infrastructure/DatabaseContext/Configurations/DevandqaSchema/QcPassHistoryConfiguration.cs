using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.DevandqaSchema;

namespace HRM.Infrastructure.DatabaseContext.Configurations.DevandqaSchema
{
    public class QcPassHistoryConfiguration : IEntityTypeConfiguration<QcPassHistory>
    {
        public void Configure(EntityTypeBuilder<QcPassHistory> entity)
        {
            entity.ToTable("qc_pass_history", "devandqa");

            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                  .HasColumnName("id")
                  .UseIdentityAlwaysColumn();

            entity.Property(x => x.QcDate).HasColumnName("qcdate");
            entity.Property(x => x.MachineId).HasColumnName("machineid").HasColumnType("citext");
            entity.Property(x => x.BatchNo).HasColumnName("batchno").HasColumnType("citext");
            entity.Property(x => x.QcRound).HasColumnName("qcround");
            entity.Property(x => x.Note).HasColumnName("note");
            entity.Property(x => x.EmployeeId).HasColumnName("employeeid");
            entity.Property(x => x.StatusQc).HasColumnName("statusqc").HasColumnType("citext");

            entity.HasIndex(x => x.QcDate).HasDatabaseName("ix_qcpasshistory_qcdate");
            entity.HasIndex(x => new { x.MachineId, x.QcDate }).HasDatabaseName("ix_qcpasshistory_machine_qcdate");
            entity.HasIndex(x => new { x.BatchNo, x.QcRound }).HasDatabaseName("ix_qcpasshistory_batch_round");
        }
    }

}
