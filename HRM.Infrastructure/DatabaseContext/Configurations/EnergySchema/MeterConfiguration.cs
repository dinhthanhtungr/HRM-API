using HRM.Domain.Entities.EnergyScheme;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Infrastructure.DatabaseContext.Configurations.EnergySchema
{
    public class MeterConfiguration: IEntityTypeConfiguration<Meter>
    {
        public void Configure(EntityTypeBuilder<Meter> entity)
        {
            entity.HasKey(e => e.MeterId).HasName("pk_energy_meters");

            entity.ToTable("meters", "energy");

            entity.HasIndex(e => new { e.GroupId, e.IsActive, e.MeterId }, "ix_energy_meters_group_active").IsDescending(false, false, true);

            entity.HasIndex(e => e.Code, "ux_energy_meters_code").IsUnique();

            entity.Property(e => e.MeterId).HasColumnName("meter_id");
            entity.Property(e => e.Code)
                .HasColumnType("citext")
                .HasColumnName("code");
            entity.Property(e => e.GroupId).HasColumnName("group_id");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Multiplier)
                .HasPrecision(10, 4)
                .HasDefaultValueSql("1.0")
                .HasColumnName("multiplier");
            entity.Property(e => e.Name)
                .HasColumnType("citext")
                .HasColumnName("name");

            entity.HasOne(d => d.Group).WithMany(p => p.Meters)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_energy_meters_group");
        }
    }
}
