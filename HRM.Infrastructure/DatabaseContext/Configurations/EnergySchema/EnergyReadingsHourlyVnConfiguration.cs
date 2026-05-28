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
    public class EnergyReadingsHourlyVnConfiguration : IEntityTypeConfiguration<EnergyReadingsHourlyVn>
    {
        public void Configure(EntityTypeBuilder<EnergyReadingsHourlyVn> entity)
        {
            entity.ToTable("readings_hourly_vn", "energy");

            entity.HasKey(x => new { x.MeterId, x.TsHourVn })
                  .HasName("pk_energy_readings_hourly_vn");

            entity.Property(x => x.MeterId).HasColumnName("meter_id");

            entity.Property(x => x.TsHourVn)
                  .HasColumnName("ts_hour_vn")
                  .HasDefaultValueSql("timezone('utc', now())");

            entity.Property(x => x.KwhImport)
                  .HasColumnName("kwh_import")
                  .HasPrecision(14, 5);

            entity.Property(x => x.Quality).HasColumnName("quality").HasColumnType("text");
            entity.Property(x => x.Source).HasColumnName("source").HasColumnType("text");

            entity.HasIndex(x => x.TsHourVn)
                  .HasDatabaseName("ix_energy_rhvn_tshour");

            entity.HasIndex(x => new { x.MeterId, x.TsHourVn })
                  .IsDescending(false, true)
                  .HasDatabaseName("ix_energy_rhvn_meter_tshour_desc");

            entity.HasOne(x => x.Meter)
                  .WithMany()
                  .HasForeignKey(x => x.MeterId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_energy_rhvn_meter");
        }
    }
}
