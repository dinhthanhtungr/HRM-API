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
    public class EnergyGroupTariffMapConfiguration : IEntityTypeConfiguration<EnergyGroupTariffMap>
    {
        public void Configure(EntityTypeBuilder<EnergyGroupTariffMap> entity)
        {
            entity.ToTable("group_tariff_map", "energy");

            entity.HasKey(x => new { x.GroupId, x.ValidFrom })
                  .HasName("pk_energy_group_tariff_map");

            entity.Property(x => x.GroupId).HasColumnName("group_id");
            entity.Property(x => x.TariffId).HasColumnName("tariff_id");

            entity.Property(x => x.ValidFrom)
                  .HasColumnName("valid_from")
                  .HasColumnType("date")
                  .HasDefaultValueSql("timezone('utc', now())");

            entity.Property(x => x.ValidTo)
                  .HasColumnName("valid_to")
                  .HasColumnType("date")
                  .HasDefaultValueSql("timezone('utc', now())");

            entity.HasIndex(x => x.TariffId)
                  .HasDatabaseName("ix_energy_gtm_tariff");

            entity.HasIndex(x => new { x.GroupId, x.ValidFrom })
                  .IsDescending(false, true)
                  .HasDatabaseName("ix_energy_gtm_group_validfrom_desc");

            entity.HasOne(x => x.Group)
                  .WithMany(g => g.EnergyGroupTariffMaps)
                  .HasForeignKey(x => x.GroupId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_energy_gtm_group");

            entity.HasOne(x => x.Tariff)
                  .WithMany(t => t.EnergyGroupTariffMaps)
                  .HasForeignKey(x => x.TariffId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_energy_gtm_tariff");
        }
    }
}
