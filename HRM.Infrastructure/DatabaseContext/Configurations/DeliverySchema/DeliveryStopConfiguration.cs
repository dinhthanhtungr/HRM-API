using HRM.Domain.Entities.DeliverySchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.DeliverySchema
{
    public sealed class DeliveryStopConfiguration : IEntityTypeConfiguration<DeliveryStop>
    {
        public void Configure(EntityTypeBuilder<DeliveryStop> entity)
        {
            entity.ToTable("DeliveryStops", "DeliveryOrder");

            entity.HasKey(e => e.Id).HasName("PK_DeliveryStops");

            entity.Property(e => e.Id)
                  .HasDefaultValueSql("gen_random_uuid()")
                  .HasColumnName("ID");

            entity.Property(e => e.StopType).IsRequired().HasColumnType("citext");
            entity.Property(e => e.Name).IsRequired().HasColumnType("citext");
            entity.Property(e => e.Address).IsRequired();
            entity.Property(e => e.Status).IsRequired().HasColumnType("citext");
            entity.Property(e => e.ContactName).HasColumnType("citext");
            entity.Property(e => e.ContactPhone).HasColumnType("citext");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasIndex(e => e.DeliveryTripId, "IX_DeliveryStops_DeliveryTripId");
            entity.HasIndex(e => e.DeliveryOrderId, "IX_DeliveryStops_DeliveryOrderId");
            entity.HasIndex(e => new { e.DeliveryTripId, e.SequenceNo }, "UX_DeliveryStops_Trip_Sequence")
                  .IsUnique();

            entity.HasOne(e => e.DeliveryTrip)
                  .WithMany(t => t.Stops)
                  .HasForeignKey(e => e.DeliveryTripId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_DeliveryStops_Trip");

            entity.HasOne(e => e.DeliveryOrder)
                  .WithMany(o => o.DeliveryStops)
                  .HasForeignKey(e => e.DeliveryOrderId)
                  .OnDelete(DeleteBehavior.SetNull)
                  .HasConstraintName("FK_DeliveryStops_Order");
        }
    }
}
