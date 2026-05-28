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
    public class EnergyMeterGroupHistoryConfiguration : IEntityTypeConfiguration<EnergyMeterGroupHistory>
    {
        public void Configure(EntityTypeBuilder<EnergyMeterGroupHistory> entity)
        {
            entity.ToTable("meter_group_history", "energy");

            entity.HasKey(x => new { x.MeterId, x.ValidFrom })
                  .HasName("pk_energy_meter_group_history");

            entity.Property(x => x.MeterId).HasColumnName("meter_id");

            entity.Property(x => x.GroupId).HasColumnName("group_id");

            entity.Property(x => x.ValidFrom)
                  .HasColumnName("valid_from")
                  .HasDefaultValueSql("timezone('utc', now())");

            entity.Property(x => x.ValidTo)
                  .HasColumnName("valid_to")
                  .HasDefaultValueSql("timezone('utc', now())");

            entity.HasIndex(x => x.MeterId)
                  .HasDatabaseName("ix_energy_mgh_meter");

            entity.HasIndex(x => new { x.MeterId, x.ValidFrom })
                  .IsDescending(false, true)
                  .HasDatabaseName("ix_energy_mgh_meter_validfrom_desc");

            entity.HasOne(x => x.Meter)
                  .WithMany()
                  .HasForeignKey(x => x.MeterId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_energy_mgh_meter");

            entity.HasOne(x => x.Group)
                  .WithMany()
                  .HasForeignKey(x => x.GroupId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_energy_mgh_group");
        }
    }
}
