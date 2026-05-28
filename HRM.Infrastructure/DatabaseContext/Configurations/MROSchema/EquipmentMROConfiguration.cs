using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.MROSchema;
public class EquipmentMROConfiguration : IEntityTypeConfiguration<EquipmentMRO>
{
    public void Configure(EntityTypeBuilder<EquipmentMRO> entity)
    {
        entity.ToTable("equipment", "mro");

        entity.HasKey(x => x.EquipmentId).HasName("pk_equipment");

        entity.Property(x => x.EquipmentId)
              .HasColumnName("equipment_id")
              .UseIdentityByDefaultColumn();

        entity.Property(x => x.EquipmentExternalId)
              .HasColumnName("equipment_externalid")
              .HasColumnType("text")
              .IsRequired();

        entity.Property(x => x.EquipmentName)
              .HasColumnName("equipment_name")
              .HasColumnType("text")
              .IsRequired();

        entity.Property(x => x.AreaExternalId).HasColumnName("area_externalid").HasColumnType("text");
        entity.Property(x => x.FactoryExternalId).HasColumnName("factory_externalid").HasColumnType("text");
        entity.Property(x => x.PartExternalId).HasColumnName("part_externalid").HasColumnType("citext");

        entity.Property(x => x.AreaId).HasColumnName("area_id");
        entity.Property(x => x.FactoryId).HasColumnName("factory_id");
        entity.Property(x => x.PartId).HasColumnName("part_id");

        entity.HasIndex(x => x.AreaExternalId).HasDatabaseName("ix_equipment_area_externalid");
        entity.HasIndex(x => x.FactoryExternalId).HasDatabaseName("ix_equipment_factory_externalid");
        entity.HasIndex(x => x.PartExternalId).HasDatabaseName("ix_equipment_part_externalid");

        entity.HasIndex(x => new { x.FactoryId, x.EquipmentExternalId })
              .HasDatabaseName("ux_equipment_factory_extid")
              .IsUnique();

        entity.HasOne(x => x.Area)
              .WithMany(a => a.Equipments)
              .HasForeignKey(x => x.AreaId)
              .IsRequired(false)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_equipment_area_id");

        entity.HasOne(x => x.Factory)
              .WithMany()
              .HasForeignKey(x => x.FactoryId)
              .IsRequired(false)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_equipment_factory_id");

        entity.HasOne(x => x.Part)
              .WithMany()
              .HasForeignKey(x => x.PartId)
              .IsRequired(false)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_equipment_part_id");
    }
}
