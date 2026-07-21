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
    public class TouWindowConfiguration : IEntityTypeConfiguration<TouWindow>
    {
        public void Configure(EntityTypeBuilder<TouWindow> entity)
        {
            entity.HasKey(e => e.WindowId).HasName("pk_energy_tou_windows");

            entity.ToTable("tou_windows", "energy");

            entity.HasIndex(e => new { e.CalendarId, e.Weekday, e.StartTime }, "ux_energy_tou_windows_cal_wd_start").IsUnique();

            entity.Property(e => e.WindowId).HasColumnName("window_id");
            entity.Property(e => e.Band).HasColumnName("band");
            entity.Property(e => e.CalendarId).HasColumnName("calendar_id");
            entity.Property(e => e.EndTime)
                .HasDefaultValueSql("timezone('utc'::text, now())")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("end_time");
            entity.Property(e => e.StartTime)
                .HasDefaultValueSql("timezone('utc'::text, now())")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("start_time");
            entity.Property(e => e.Weekday).HasColumnName("weekday");

            entity.HasOne(d => d.Calendar).WithMany(p => p.TouWindows)
                .HasForeignKey(d => d.CalendarId)
                .HasConstraintName("fk_energy_tou_windows_calendar");
        }
    }
}
