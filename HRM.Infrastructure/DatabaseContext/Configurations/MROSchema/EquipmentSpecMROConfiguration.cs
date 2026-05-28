using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.MROSchema;
public class EquipmentSpecMROConfiguration : IEntityTypeConfiguration<EquipmentSpecMRO>
{
    public void Configure(EntityTypeBuilder<EquipmentSpecMRO> entity)
    {
        entity.ToTable("equipment_specs", "mro");

        entity.HasKey(x => x.SpecId).HasName("pk_equipment_specs");

        entity.Property(x => x.SpecId)
              .UseIdentityByDefaultColumn()
              .HasColumnName("spec_id");

        entity.Property(x => x.EquipmentId).HasColumnName("equipment_id");

        entity.Property(x => x.SpecKey).HasColumnName("spec_key").HasColumnType("text");
        entity.Property(x => x.SpecValue).HasColumnName("spec_value").HasColumnType("text");
        entity.Property(x => x.Unit).HasColumnName("unit").HasColumnType("text");
        entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        entity.Property(x => x.EnteredAt).HasColumnName("entered_at").HasColumnType("timestamp");
        entity.Property(x => x.EnteredBy).HasColumnName("entered_by");

        entity.HasIndex(x => x.EquipmentId)
              .HasDatabaseName("ix_equipment_specs_equipment_id");

        entity.HasOne(d => d.Equipment)
              .WithMany()
              .HasForeignKey(d => d.EquipmentId)
              .OnDelete(DeleteBehavior.Cascade)
              .HasConstraintName("fk_equipment_specs_equipment");
    }
}
