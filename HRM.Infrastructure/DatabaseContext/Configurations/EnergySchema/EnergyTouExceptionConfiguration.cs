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
    public class EnergyTouExceptionConfiguration : IEntityTypeConfiguration<EnergyTouException>
    {
        public void Configure(EntityTypeBuilder<EnergyTouException> entity)
        {
            entity.ToTable("tou_exceptions", "energy");

            entity.HasKey(x => x.ExceptionId)
                  .HasName("pk_energy_tou_exceptions");

            entity.Property(x => x.ExceptionId).HasColumnName("exception_id");
            entity.Property(x => x.CalendarId).HasColumnName("calendar_id");

            entity.Property(x => x.TheDate)
                  .HasColumnName("the_date")
                  .HasColumnType("date");

            entity.Property(x => x.StartTime)
                  .HasColumnName("start_time")
                  .HasDefaultValueSql("timezone('utc', now())");

            entity.Property(x => x.EndTime)
                  .HasColumnName("end_time")
                  .HasDefaultValueSql("timezone('utc', now())");

            entity.Property(x => x.Band).HasColumnName("band").HasColumnType("text");
            entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");

            entity.HasIndex(x => new { x.CalendarId, x.TheDate })
                  .HasDatabaseName("ix_energy_tou_exc_calendar_date");

            entity.HasIndex(x => new { x.CalendarId, x.TheDate, x.Band, x.StartTime })
                  .IsUnique()
                  .HasDatabaseName("ux_energy_tou_exc_unique_slot");

            entity.HasOne(x => x.Calendar)
                  .WithMany(c => c.Exceptions)
                  .HasForeignKey(x => x.CalendarId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_energy_tou_exc_calendar");
        }
    }
}
