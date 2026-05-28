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
    public class EnergyMeterConfiguration : IEntityTypeConfiguration<EnergyMeter>
    {
        public void Configure(EntityTypeBuilder<EnergyMeter> entity)
        {
            entity.ToTable("meters", "energy");

            entity.HasKey(x => x.MeterId)
                  .HasName("pk_energy_meters");

            entity.Property(x => x.MeterId)
                  .HasColumnName("meter_id")
                  .UseIdentityByDefaultColumn();

            entity.Property(x => x.Code)
                  .HasColumnName("code")
                  .HasColumnType("citext")
                  .IsRequired();

            entity.Property(x => x.Name)
                  .HasColumnName("name")
                  .HasColumnType("citext")
                  .IsRequired();

            entity.Property(x => x.Multiplier)
                  .HasColumnName("multiplier")
                  .HasPrecision(10, 4)
                  .HasDefaultValue(1.0000m);

            entity.Property(x => x.IsActive)
                  .HasColumnName("is_active")
                  .HasDefaultValue(true);

            entity.Property(x => x.GroupId)
                  .HasColumnName("group_id")
                  .IsRequired();

            entity.HasIndex(x => x.Code)
                  .IsUnique()
                  .HasDatabaseName("ux_energy_meters_code");

            entity.HasIndex(x => new { x.GroupId, x.IsActive, x.MeterId })
                  .IsDescending(false, false, true)
                  .HasDatabaseName("ix_energy_meters_group_active");

            entity.HasOne(x => x.Group)
                  .WithMany(g => g.Meters)
                  .HasForeignKey(x => x.GroupId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .HasConstraintName("fk_energy_meters_group");
        }
    }
}
