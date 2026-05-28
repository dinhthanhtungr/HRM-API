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
    public class EnergyTouWindowConfiguration : IEntityTypeConfiguration<EnergyTouWindow>
    {
        public void Configure(EntityTypeBuilder<EnergyTouWindow> entity)
        {
            entity.ToTable("tou_windows", "energy");

            entity.HasKey(x => x.WindowId)
                  .HasName("pk_energy_tou_windows");

            entity.Property(x => x.WindowId)
                  .HasColumnName("window_id")
                  .UseIdentityByDefaultColumn();

            entity.Property(x => x.CalendarId).HasColumnName("calendar_id");
            entity.Property(x => x.Weekday).HasColumnName("weekday");

            entity.Property(x => x.StartTime)
                  .HasColumnName("start_time")
                  .HasDefaultValueSql("timezone('utc', now())");

            entity.Property(x => x.EndTime)
                  .HasColumnName("end_time")
                  .HasDefaultValueSql("timezone('utc', now())");

            entity.Property(x => x.Band).HasColumnName("band").HasColumnType("text");

            entity.HasOne(x => x.Calendar)
                  .WithMany()
                  .HasForeignKey(x => x.CalendarId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_energy_tou_windows_calendar");

            entity.HasIndex(x => new { x.CalendarId, x.Weekday, x.StartTime })
                  .IsUnique()
                  .HasDatabaseName("ux_energy_tou_windows_cal_wd_start");
        }
    }
}
