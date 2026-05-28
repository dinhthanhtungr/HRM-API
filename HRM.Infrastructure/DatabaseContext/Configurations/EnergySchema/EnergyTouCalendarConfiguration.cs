using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Entities.EnergyScheme;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs.Configurations.EnergySchema
{
    public class EnergyTouCalendarConfiguration : IEntityTypeConfiguration<EnergyTouCalendar>
    {
        public void Configure(EntityTypeBuilder<EnergyTouCalendar> entity)
        {
            entity.ToTable("tou_calendar", "energy");

            entity.HasKey(x => x.CalendarId)
                  .HasName("pk_energy_tou_calendar");

            entity.Property(x => x.CalendarId).HasColumnName("calendar_id");

            entity.Property(x => x.Code)
                  .HasColumnName("code")
                  .HasColumnType("text")
                  .IsRequired();

            entity.Property(x => x.Tz)
                  .HasColumnName("tz")
                  .HasColumnType("text");

            entity.Property(x => x.StartDate)
                  .HasColumnName("start_date")
                  .HasColumnType("date")
                  .HasDefaultValueSql("timezone('utc', now())");

            entity.Property(x => x.EndDate)
                  .HasColumnName("end_date")
                  .HasColumnType("date")
                  .HasDefaultValueSql("timezone('utc', now())");

            entity.HasIndex(x => x.Code)
                  .IsUnique()
                  .HasDatabaseName("ux_energy_tou_calendar_code");
        }
    }
}
