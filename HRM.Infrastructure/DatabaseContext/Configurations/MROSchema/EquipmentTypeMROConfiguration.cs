using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.MROSchema;
public class EquipmentTypeMROConfiguration : IEntityTypeConfiguration<EquipmentTypeMRO>
{
    public void Configure(EntityTypeBuilder<EquipmentTypeMRO> entity)
    {
        entity.ToTable("equipmenttype", "mro");

        entity.HasKey(x => x.EquipmentTypeId).HasName("pk_equipmenttype");

        entity.Property(x => x.EquipmentTypeId)
              .UseIdentityByDefaultColumn()
              .HasColumnName("equipmenttype_id");

        entity.Property(x => x.EquipmentTypeName)
              .HasColumnName("equipmenttype_name")
              .HasColumnType("text")
              .IsRequired();

        entity.HasIndex(x => x.EquipmentTypeName)
              .HasDatabaseName("ix_equipmenttype_name");
    }
}
