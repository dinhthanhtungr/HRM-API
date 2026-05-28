using HRM.Domain.Entities.DeliverySchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.DeliverySchema
{
    public sealed class DeliveryTripOrderConfiguration : IEntityTypeConfiguration<DeliveryTripOrder>
    {
        public void Configure(EntityTypeBuilder<DeliveryTripOrder> entity)
        {
            entity.ToTable("DeliveryTripOrders", "DeliveryOrder");

            entity.HasKey(e => new { e.DeliveryTripId, e.DeliveryOrderId })
                  .HasName("PK_DeliveryTripOrders");

            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasIndex(e => e.DeliveryOrderId, "IX_DeliveryTripOrders_DeliveryOrderId");

            entity.HasOne(e => e.DeliveryTrip)
                  .WithMany(t => t.Orders)
                  .HasForeignKey(e => e.DeliveryTripId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_DeliveryTripOrders_Trip");

            entity.HasOne(e => e.DeliveryOrder)
                  .WithMany(o => o.DeliveryTripOrders)
                  .HasForeignKey(e => e.DeliveryOrderId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("FK_DeliveryTripOrders_Order");
        }
    }
}
