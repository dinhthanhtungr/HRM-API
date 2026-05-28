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
    public class ShiftsEventConfiguration : IEntityTypeConfiguration<ShiftsEvent>
    {
        public void Configure(EntityTypeBuilder<ShiftsEvent> entity)
        {
            entity.ToTable("shiftsevent", "historyrecords");
            entity.HasNoKey();

            entity.Property(x => x.EventId).HasColumnName("eventid");
            entity.Property(x => x.EventIdName).HasColumnName("eventidname").HasColumnType("citext");
            entity.Property(x => x.Note).HasColumnName("note");

            entity.HasIndex(x => x.EventIdName).HasDatabaseName("ix_shiftsevent_name");
        }
    }
}
