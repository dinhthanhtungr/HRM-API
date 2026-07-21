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
    public class TariffBandRateConfiguration : IEntityTypeConfiguration<TariffBandRate>
    {
        public void Configure(EntityTypeBuilder<TariffBandRate> entity)
        {
            entity.HasKey(e => new { e.VersionId, e.Band }).HasName("pk_energy_tariff_band_rates");

            entity.ToTable("tariff_band_rates", "energy");

            entity.HasIndex(e => e.VersionId, "ix_energy_tariff_band_rates_version");

            entity.Property(e => e.VersionId).HasColumnName("version_id");
            entity.Property(e => e.Band)
                .HasColumnType("citext")
                .HasColumnName("band");
            entity.Property(e => e.PriceVndPerKwh)
                .HasPrecision(12, 4)
                .HasColumnName("price_vnd_per_kwh");

            entity.HasOne(d => d.Version).WithMany(p => p.TariffBandRates)
                .HasForeignKey(d => d.VersionId)
                .HasConstraintName("fk_energy_tariff_band_rates_version");
        }
    }
}
