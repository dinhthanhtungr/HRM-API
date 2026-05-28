using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.MROSchema;

public class ZoneMROConfiguration : IEntityTypeConfiguration<ZoneMRO>
{
    public void Configure(EntityTypeBuilder<ZoneMRO> entity)
    {
        entity.HasKey(e => e.ZoneId).HasName("PK__Zone__3214EC07A98DEC4E");
        entity.Property(x => x.ZoneId).UseIdentityAlwaysColumn();

        entity.ToTable("zones", "mro");

        entity.Property(x => x.ZoneId).HasColumnName("zone_id");
        entity.Property(x => x.WarehouseId).HasColumnName("warehouse_id");
        entity.Property(x => x.ZoneExternalId)
              .HasColumnName("zone_external_id")
              .HasColumnType("text")
              .IsRequired();
        entity.Property(x => x.ZoneName)
              .HasColumnName("zone_name")
              .HasColumnType("text")
              .IsRequired();

        entity.HasIndex(x => x.WarehouseId)
              .HasDatabaseName("ix_zones_warehouse_id");

        entity.HasOne(z => z.Warehouse)
              .WithMany(w => w.Zones)
              .HasForeignKey(z => z.WarehouseId)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_zones_warehouse_id");

        entity.HasIndex(x => new { x.WarehouseId, x.ZoneExternalId })
              .HasDatabaseName("ux_zones_warehouse_external")
              .IsUnique();
    }
}
