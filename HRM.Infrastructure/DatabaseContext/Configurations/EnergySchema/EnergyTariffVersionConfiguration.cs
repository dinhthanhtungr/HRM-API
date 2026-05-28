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
    public class EnergyTariffVersionConfiguration : IEntityTypeConfiguration<EnergyTariffVersion>
    {
        public void Configure(EntityTypeBuilder<EnergyTariffVersion> entity)
        {
            entity.ToTable("tariff_versions", "energy");

            entity.HasKey(x => x.VersionId)
                  .HasName("pk_energy_tariff_versions");

            entity.Property(x => x.VersionId)
                  .HasColumnName("version_id")
                  .UseIdentityByDefaultColumn();

            entity.Property(x => x.TariffId)
                  .HasColumnName("tariff_id")
                  .IsRequired();

            entity.Property(x => x.ValidFrom)
                  .HasColumnName("valid_from")
                  .HasColumnType("date")
                  .HasDefaultValueSql("timezone('utc', now())")
                  .IsRequired();

            entity.Property(x => x.ValidTo)
                  .HasColumnName("valid_to")
                  .HasColumnType("date")
                  .HasDefaultValueSql("timezone('utc', now())");

            entity.Property(x => x.VatRate)
                  .HasColumnName("vat_rate")
                  .HasPrecision(6, 4)
                  .HasDefaultValue(0m);

            entity.Property(x => x.FuelAdjVndPerKwh)
                  .HasColumnName("fuel_adj_vnd_per_kwh")
                  .HasPrecision(12, 4)
                  .HasDefaultValue(0m);

            entity.Property(x => x.ServiceFixedVndPerMonth)
                  .HasColumnName("service_fixed_vnd_per_month")
                  .HasPrecision(14, 2)
                  .HasDefaultValue(0m);

            entity.Property(x => x.DemandRateVndPerKw)
                  .HasColumnName("demand_rate_vnd_per_kw")
                  .HasPrecision(14, 2)
                  .HasDefaultValue(0m);

            entity.HasIndex(x => x.TariffId)
                  .HasDatabaseName("ix_energy_tariff_versions_tariff_id");

            entity.HasIndex(x => new { x.TariffId, x.ValidFrom })
                  .IsUnique()
                  .HasDatabaseName("ux_energy_tariff_versions_tariff_validfrom");

            entity.HasOne(x => x.Tariff)
                  .WithMany(t => t.Versions)
                  .HasForeignKey(x => x.TariffId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_energy_tariff_versions_tariff");
        }
    }
}
