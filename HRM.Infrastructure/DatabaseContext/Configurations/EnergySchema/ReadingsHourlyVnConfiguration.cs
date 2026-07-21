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
    public class ReadingsHourlyVnConfiguration : IEntityTypeConfiguration<ReadingsHourlyVn>
    {
        public void Configure(EntityTypeBuilder<ReadingsHourlyVn> entity)
        {
            entity.HasKey(e => new { e.MeterId, e.TsHourVn }).HasName("pk_energy_readings_hourly_vn");

            entity.ToTable("readings_hourly_vn", "energy");

            entity.HasIndex(e => new { e.MeterId, e.TsHourVn }, "ix_energy_rhvn_meter_tshour_desc").IsDescending(false, true);

            entity.HasIndex(e => e.TsHourVn, "ix_energy_rhvn_tshour");

            entity.Property(e => e.MeterId).HasColumnName("meter_id");
            entity.Property(e => e.TsHourVn)
                .HasDefaultValueSql("timezone('utc'::text, now())")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("ts_hour_vn");
            entity.Property(e => e.KwhImport)
                .HasPrecision(14, 5)
                .HasColumnName("kwh_import");
            entity.Property(e => e.Quality).HasColumnName("quality");
            entity.Property(e => e.Source).HasColumnName("source");

            entity.HasOne(d => d.Meter).WithMany(p => p.ReadingsHourlyVns)
                .HasForeignKey(d => d.MeterId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_energy_rhvn_meter");
        }
    }
}
