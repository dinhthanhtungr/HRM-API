using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.MROSchema;
public class EquipmentDetailMROConfiguration : IEntityTypeConfiguration<EquipmentDetailMRO>
{
    public void Configure(EntityTypeBuilder<EquipmentDetailMRO> entity)
    {
        entity.ToTable("equipment_details", "mro");

        entity.HasKey(x => x.EquipmentId).HasName("pk_equipment_details");

        entity.Property(x => x.EquipmentId)
              .HasColumnName("equipment_id")
              .ValueGeneratedNever();

        entity.Property(x => x.SerialNo).HasColumnName("serial_no").HasColumnType("text");
        entity.Property(x => x.Manufacturer).HasColumnName("manufacturer").HasColumnType("text");
        entity.Property(x => x.Model).HasColumnName("model").HasColumnType("text");

        entity.Property(x => x.PurchaseDate).HasColumnName("purchase_date").HasColumnType("date");
        entity.Property(x => x.CommissioningDate).HasColumnName("commissioning_date").HasColumnType("date");
        entity.Property(x => x.WarrantyUntil).HasColumnName("warranty_until").HasColumnType("date");

        entity.Property(x => x.Notes).HasColumnName("notes").HasColumnType("text");

        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp");
        entity.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        entity.Property(x => x.EquipmentTypeId).HasColumnName("equipmenttype_id");

        entity.HasIndex(x => x.EquipmentTypeId)
              .HasDatabaseName("ix_equipment_details_equipmenttype_id");

        entity.HasOne(d => d.Equipment)
              .WithMany(p => p.EquipmentDetails)
              .HasForeignKey(d => d.EquipmentId)
              .OnDelete(DeleteBehavior.Cascade)
              .HasConstraintName("fk_equipment_details_equipment");

        entity.HasOne(d => d.EquipmentType)
              .WithMany(t => t.EquipmentDetails)
              .HasForeignKey(d => d.EquipmentTypeId)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_equipment_details_equipmenttype");
    }
}
