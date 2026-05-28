using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.MROSchema;

public class WarehouseMROConfiguration : IEntityTypeConfiguration<WarehouseMRO>
{
    public void Configure(EntityTypeBuilder<WarehouseMRO> entity)
    {
        entity.HasKey(e => e.WarehouseId).HasName("PK__Warehouses__3214EC07A98DEC4E");
        entity.Property(x => x.WarehouseId).UseIdentityAlwaysColumn();

        entity.ToTable("warehouses", "mro");

        entity.Property(x => x.WarehouseId).HasColumnName("warehouse_id");
        entity.Property(x => x.WarehouseExternalId)
              .HasColumnName("warehouse_external_id")
              .HasColumnType("text")
              .IsRequired();
        entity.Property(x => x.WarehouseName)
              .HasColumnName("warehouse_name")
              .HasColumnType("text")
              .IsRequired();

        entity.HasIndex(x => x.WarehouseExternalId)
              .HasDatabaseName("ix_warehouses_external_id")
              .IsUnique();
    }
}
