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
    internal class RegisterSnapshotConfiguration : IEntityTypeConfiguration<RegisterSnapshot>
    {
        public void Configure(EntityTypeBuilder<RegisterSnapshot> entity)
        {
            entity.HasKey(e => new { e.MeterId, e.TsUtc }).HasName("pk_energy_register_snapshots");

            entity.ToTable("register_snapshots", "energy");

            entity.HasIndex(e => new { e.MeterId, e.TsUtc }, "ix_energy_register_snapshots_meter_ts").IsDescending(false, true);

            entity.Property(e => e.MeterId).HasColumnName("meter_id");
            entity.Property(e => e.TsUtc)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("ts_utc");
            entity.Property(e => e.KwhTotal)
                .HasPrecision(18, 4)
                .HasColumnName("kwh_total");
            entity.Property(e => e.Source)
                .HasColumnType("citext")
                .HasColumnName("source");

            entity.HasOne(d => d.Meter).WithMany(p => p.RegisterSnapshots)
                .HasForeignKey(d => d.MeterId)
                .HasConstraintName("fk_energy_register_snapshots_meter");
        }
    }
}
