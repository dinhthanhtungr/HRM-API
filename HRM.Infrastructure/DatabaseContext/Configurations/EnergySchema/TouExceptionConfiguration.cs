using HRM.Domain.Entities.EnergyScheme;
using HRM.Domain.Entities.HistoryRecordSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Infrastructure.DatabaseContext.Configurations.EnergySchema
{
    public class TouExceptionConfiguration : IEntityTypeConfiguration<TouException>
    {
        public void Configure(EntityTypeBuilder<TouException> entity)
        {
            entity.HasKey(e => e.ExceptionId).HasName("pk_energy_tou_exceptions");

            entity.ToTable("tou_exceptions", "energy");

            entity.HasIndex(e => new { e.CalendarId, e.TheDate }, "ix_energy_tou_exc_calendar_date");

            entity.HasIndex(e => new { e.CalendarId, e.TheDate, e.Band, e.StartTime }, "ux_energy_tou_exc_unique_slot").IsUnique();

            entity.Property(e => e.ExceptionId).HasColumnName("exception_id");
            entity.Property(e => e.Band).HasColumnName("band");
            entity.Property(e => e.CalendarId).HasColumnName("calendar_id");
            entity.Property(e => e.EndTime)
                .HasDefaultValueSql("timezone('utc'::text, now())")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("end_time");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.StartTime)
                .HasDefaultValueSql("timezone('utc'::text, now())")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("start_time");
            entity.Property(e => e.TheDate).HasColumnName("the_date");

            entity.HasOne(d => d.Calendar).WithMany(p => p.TouExceptions)
                .HasForeignKey(d => d.CalendarId)
                .HasConstraintName("fk_energy_tou_exc_calendar");
        }
    }
}
