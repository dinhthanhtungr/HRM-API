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
    public class EnergyReadingsHourlyConfiguration : IEntityTypeConfiguration<EnergyReadingsHourly>
    {
        public void Configure(EntityTypeBuilder<EnergyReadingsHourly> entity)
        {
            entity.ToTable("readings_hourly", "energy");

            entity.HasKey(x => new { x.MeterId, x.TsUtc })
                  .HasName("pk_energy_readings_hourly");

            entity.Property(x => x.MeterId).HasColumnName("meter_id");

            entity.Property(x => x.TsUtc)
                  .HasColumnName("ts_utc")
                  .HasDefaultValueSql("timezone('utc', now())");

            entity.Property(x => x.KwhImport)
                  .HasColumnName("kwh_import")
                  .HasPrecision(14, 5);

            entity.Property(x => x.Quality).HasColumnName("quality").HasColumnType("text");
            entity.Property(x => x.Source).HasColumnName("source").HasColumnType("text");

            entity.HasIndex(x => new { x.MeterId, x.TsUtc })
                  .IsDescending(false, true)
                  .HasDatabaseName("ix_energy_rh_meter_ts_desc");

            entity.HasOne(x => x.Meter)
                  .WithMany()
                  .HasForeignKey(x => x.MeterId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_energy_rh_meter");
        }
    }
}
