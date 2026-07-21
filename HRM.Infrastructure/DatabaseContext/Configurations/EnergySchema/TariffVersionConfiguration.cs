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
    public class TariffVersionConfiguration : IEntityTypeConfiguration<TariffVersion>
    {
        public void Configure(EntityTypeBuilder<TariffVersion> entity)
        {
            entity.HasKey(e => e.VersionId).HasName("pk_energy_tariff_versions");

            entity.ToTable("tariff_versions", "energy");

            entity.HasIndex(e => e.TariffId, "ix_energy_tariff_versions_tariff_id");

            entity.HasIndex(e => new { e.TariffId, e.ValidFrom }, "ux_energy_tariff_versions_tariff_validfrom").IsUnique();

            entity.Property(e => e.VersionId).HasColumnName("version_id");
            entity.Property(e => e.DemandRateVndPerKw)
                .HasPrecision(14, 2)
                .HasColumnName("demand_rate_vnd_per_kw");
            entity.Property(e => e.FuelAdjVndPerKwh)
                .HasPrecision(12, 4)
                .HasColumnName("fuel_adj_vnd_per_kwh");
            entity.Property(e => e.ServiceFixedVndPerMonth)
                .HasPrecision(14, 2)
                .HasColumnName("service_fixed_vnd_per_month");
            entity.Property(e => e.TariffId).HasColumnName("tariff_id");
            entity.Property(e => e.ValidFrom)
                .HasDefaultValueSql("timezone('utc'::text, now())")
                .HasColumnName("valid_from");
            entity.Property(e => e.ValidTo)
                .HasDefaultValueSql("timezone('utc'::text, now())")
                .HasColumnName("valid_to");
            entity.Property(e => e.VatRate)
                .HasPrecision(6, 4)
                .HasColumnName("vat_rate");

            entity.HasOne(d => d.Tariff).WithMany(p => p.TariffVersions)
                .HasForeignKey(d => d.TariffId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_energy_tariff_versions_tariff");
        }
    }
}
