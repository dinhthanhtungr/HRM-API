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
    public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
    {
        public void Configure(EntityTypeBuilder<Shift> entity)
        {
            entity.ToTable("shifts", "historyrecords");
            entity.HasNoKey();

            entity.Property(x => x.ShiftId).HasColumnName("shiftid");
            entity.Property(x => x.ShiftName).HasColumnName("shiftname").HasColumnType("citext");
            entity.Property(x => x.Note).HasColumnName("note");

            entity.HasIndex(x => x.ShiftName).HasDatabaseName("ix_shifts_name");
        }
    }
}
