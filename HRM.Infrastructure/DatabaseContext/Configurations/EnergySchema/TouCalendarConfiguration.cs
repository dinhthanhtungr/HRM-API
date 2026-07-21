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
    public class TouCalendarConfiguration : IEntityTypeConfiguration<TouCalendar>
    {
        public void Configure(EntityTypeBuilder<TouCalendar> entity)
        {
            entity.HasKey(e => e.CalendarId).HasName("pk_energy_tou_calendar");

            entity.ToTable("tou_calendar", "energy");

            entity.HasIndex(e => e.Code, "ux_energy_tou_calendar_code").IsUnique();

            entity.Property(e => e.CalendarId).HasColumnName("calendar_id");
            entity.Property(e => e.Code).HasColumnName("code");
            entity.Property(e => e.EndDate)
                .HasDefaultValueSql("timezone('utc'::text, now())")
                .HasColumnName("end_date");
            entity.Property(e => e.StartDate)
                .HasDefaultValueSql("timezone('utc'::text, now())")
                .HasColumnName("start_date");
            entity.Property(e => e.Tz).HasColumnName("tz");
        }
    }
}
