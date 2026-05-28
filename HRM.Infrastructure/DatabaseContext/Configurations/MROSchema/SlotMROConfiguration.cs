using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HRM.Domain.Entities.MROSchema;


public class SlotMROConfiguration : IEntityTypeConfiguration<SlotMRO>
{
    public void Configure(EntityTypeBuilder<SlotMRO> entity)
    {
        entity.HasKey(e => e.SlotId).HasName("PK__Slots__3214EC07A98DEC4E");
        entity.Property(x => x.SlotId).UseIdentityAlwaysColumn();

        entity.ToTable("slots", "mro");
        entity.Property(x => x.SlotId).HasColumnName("slot_id");
        entity.Property(x => x.RackId).HasColumnName("rack_id");
        entity.Property(x => x.SlotExternalId)
              .HasColumnName("slot_external_id")
              .HasColumnType("text")
              .IsRequired();
        entity.Property(x => x.SlotName)
              .HasColumnName("slot_name")
              .HasColumnType("text")
              .IsRequired();

        entity.Property(x => x.CapacityQty)
              .HasColumnName("capacity_qty")
              .HasColumnType("integer")
              .HasDefaultValue(0);

        entity.Property(x => x.CountToCapacity)
              .HasColumnName("count_to_capacity")
              .HasColumnType("boolean")
              .HasDefaultValue(true);

        entity.HasIndex(x => x.RackId).HasDatabaseName("ix_slots_rack_id");

        entity.HasOne(s => s.Rack)
              .WithMany(r => r.Slots)
              .HasForeignKey(s => s.RackId)
              .OnDelete(DeleteBehavior.Restrict)
              .HasConstraintName("fk_slots_rack_id");

        entity.HasIndex(x => new { x.RackId, x.SlotExternalId })
              .HasDatabaseName("ux_slots_rack_external")
              .IsUnique();
    }
}
