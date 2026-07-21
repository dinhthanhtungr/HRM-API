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
    public class ReadingsHourlyConfiguration : IEntityTypeConfiguration<ReadingsHourly>
    {
        public void Configure(EntityTypeBuilder<ReadingsHourly> entity)
        {
            entity.HasKey(e => new { e.MeterId, e.TsUtc }).HasName("pk_energy_readings_hourly");

            entity.ToTable("readings_hourly", "energy");

            entity.HasIndex(e => new { e.MeterId, e.TsUtc }, "ix_energy_rh_meter_ts_desc").IsDescending(false, true);

            entity.Property(e => e.MeterId).HasColumnName("meter_id");
            entity.Property(e => e.TsUtc)
                .HasDefaultValueSql("timezone('utc'::text, now())")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("ts_utc");
            entity.Property(e => e.KwhImport)
                .HasPrecision(14, 5)
                .HasColumnName("kwh_import");
            entity.Property(e => e.Quality).HasColumnName("quality");
            entity.Property(e => e.Source).HasColumnName("source");

            entity.HasOne(d => d.Meter).WithMany(p => p.ReadingsHourlies)
                .HasForeignKey(d => d.MeterId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_energy_rh_meter");
        }
    }
}
