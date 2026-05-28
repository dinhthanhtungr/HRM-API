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
    public class EnergyRegisterSnapshotConfiguration : IEntityTypeConfiguration<EnergyRegisterSnapshot>
    {
        public void Configure(EntityTypeBuilder<EnergyRegisterSnapshot> entity)
        {
            entity.ToTable("register_snapshots", "energy");

            entity.HasKey(x => new { x.MeterId, x.TsUtc })
                  .HasName("pk_energy_register_snapshots");

            entity.Property(x => x.MeterId).HasColumnName("meter_id");

            entity.Property(x => x.TsUtc).HasColumnName("ts_utc");

            entity.Property(x => x.KwhTotal)
                  .HasColumnName("kwh_total")
                  .HasPrecision(18, 4)
                  .IsRequired();

            entity.Property(x => x.Source)
                  .HasColumnName("source")
                  .HasColumnType("citext");

            entity.HasIndex(x => new { x.MeterId, x.TsUtc })
                  .IsDescending(false, true)
                  .HasDatabaseName("ix_energy_register_snapshots_meter_ts");

            entity.HasOne(x => x.Meter)
                  .WithMany()
                  .HasForeignKey(x => x.MeterId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_energy_register_snapshots_meter");
        }
    }
}
