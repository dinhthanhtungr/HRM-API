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
    public class GroupTariffMapConfiguration : IEntityTypeConfiguration<GroupTariffMap>
    {
        public void Configure(EntityTypeBuilder<GroupTariffMap> entity)
        {
            entity.HasKey(e => new { e.GroupId, e.ValidFrom }).HasName("pk_energy_group_tariff_map");

            entity.ToTable("group_tariff_map", "energy");

            entity.HasIndex(e => new { e.GroupId, e.ValidFrom }, "ix_energy_gtm_group_validfrom_desc").IsDescending(false, true);

            entity.HasIndex(e => e.TariffId, "ix_energy_gtm_tariff");

            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.ValidFrom)
                .HasDefaultValueSql("timezone('utc'::text, now())")
                .HasColumnName("valid_from");
            entity.Property(e => e.TariffId).HasColumnName("tariff_id");
            entity.Property(e => e.ValidTo)
                .HasDefaultValueSql("timezone('utc'::text, now())")
                .HasColumnName("valid_to");

            entity.HasOne(d => d.Group).WithMany(p => p.GroupTariffMaps)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_energy_gtm_group");

            entity.HasOne(d => d.Tariff).WithMany(p => p.GroupTariffMaps)
                .HasForeignKey(d => d.TariffId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_energy_gtm_tariff");
        }
    }
}
