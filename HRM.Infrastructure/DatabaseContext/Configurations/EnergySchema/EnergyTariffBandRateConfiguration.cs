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
    public class EnergyTariffBandRateConfiguration : IEntityTypeConfiguration<EnergyTariffBandRate>
    {
        public void Configure(EntityTypeBuilder<EnergyTariffBandRate> entity)
        {
            entity.ToTable("tariff_band_rates", "energy");

            entity.HasKey(x => new { x.VersionId, x.Band })
                  .HasName("pk_energy_tariff_band_rates");

            entity.Property(x => x.VersionId).HasColumnName("version_id");

            entity.Property(x => x.Band)
                  .HasColumnName("band")
                  .HasColumnType("citext")
                  .IsRequired();

            entity.Property(x => x.PriceVndPerKwh)
                  .HasColumnName("price_vnd_per_kwh")
                  .HasPrecision(12, 4)
                  .IsRequired();

            entity.HasIndex(x => x.VersionId)
                  .HasDatabaseName("ix_energy_tariff_band_rates_version");

            entity.HasOne(x => x.Version)
                  .WithMany(v => v.BandRates)
                  .HasForeignKey(x => x.VersionId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_energy_tariff_band_rates_version");
        }
    }
}
