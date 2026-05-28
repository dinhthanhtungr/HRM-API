using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.MROSchema;
public class MovementMROConfiguration : IEntityTypeConfiguration<MovementMRO>
{
    public void Configure(EntityTypeBuilder<MovementMRO> entity)
    {
        entity.ToTable("movements", "mro");
        entity.HasKey(x => x.MovementId).HasName("pk_movements");

        entity.Property(x => x.MovementId)
              .HasColumnName("movement_id")
              .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(x => x.MaterialId).HasColumnName("material_id").IsRequired();
        entity.Property(x => x.FromSlotId).HasColumnName("fromslot_id");
        entity.Property(x => x.ToSlotId).HasColumnName("toslot_id");
        entity.Property(x => x.Qty).HasColumnName("qty").IsRequired();
        entity.Property(x => x.MovedAt).HasColumnName("moved_at");
        entity.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        entity.Property(x => x.RequestExternal).HasColumnName("requestexternal").HasColumnType("text");
        entity.Property(x => x.MoveBy).HasColumnName("move_by").HasColumnType("text");

        entity.HasIndex(x => new { x.MaterialId, x.MovedAt, x.MovementId })
              .IsDescending(false, true, true)
              .HasDatabaseName("ix_movements_material_moved_desc");

        entity.HasIndex(x => x.FromSlotId).HasDatabaseName("ix_movements_fromslot");
        entity.HasIndex(x => x.ToSlotId).HasDatabaseName("ix_movements_toslot");

        entity.HasOne(x => x.Material).WithMany()
              .HasForeignKey(x => x.MaterialId)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_MovementMRO_material");

        entity.HasOne(x => x.SlotMROFrom).WithMany()
              .HasForeignKey(x => x.FromSlotId)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_MovementMRO_SlotMROFrom");

        entity.HasOne(x => x.SlotMROTo).WithMany()
              .HasForeignKey(x => x.ToSlotId)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_MovementMRO_SlotMROTo");
    }
}
