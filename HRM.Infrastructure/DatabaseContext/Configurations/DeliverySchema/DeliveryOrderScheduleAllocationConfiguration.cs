using HRM.Domain.Entities.DeliverySchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.Configurations.DeliverySchema;

public sealed class DeliveryOrderScheduleAllocationConfiguration : IEntityTypeConfiguration<DeliveryOrderScheduleAllocation>
{
    public void Configure(EntityTypeBuilder<DeliveryOrderScheduleAllocation> b)
    {
        b.ToTable("DeliveryOrderScheduleAllocations", "DeliveryOrder", t =>
            t.HasCheckConstraint("CK_DeliveryScheduleAllocation_Positive", "\"Quantity\" > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.CreatedDate).HasColumnType("timestamp without time zone");
        b.HasIndex(x => new { x.DeliveryOrderDetailId, x.MerchandiseOrderDeliveryScheduleId })
            .IsUnique().HasFilter("\"IsActive\" = TRUE AND \"MerchandiseOrderDeliveryScheduleId\" IS NOT NULL");
        b.HasIndex(x => x.DeliveryOrderDetailId).IsUnique()
            .HasFilter("\"IsActive\" = TRUE AND \"MerchandiseOrderDeliveryScheduleId\" IS NULL");
        b.HasIndex(x => x.MerchandiseOrderDeliveryScheduleId);
        b.HasOne(x => x.DeliveryOrderDetail).WithMany().HasForeignKey(x => x.DeliveryOrderDetailId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Schedule).WithMany().HasForeignKey(x => x.MerchandiseOrderDeliveryScheduleId).OnDelete(DeleteBehavior.Restrict);
    }
}
