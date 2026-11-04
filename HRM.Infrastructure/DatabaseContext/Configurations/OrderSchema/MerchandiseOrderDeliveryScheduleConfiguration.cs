using HRM.Domain.Entities.OrderSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs.Configurations.OrderSchema;

public sealed class MerchandiseOrderDeliveryScheduleConfiguration : IEntityTypeConfiguration<MerchandiseOrderDeliverySchedule>
{
    public void Configure(EntityTypeBuilder<MerchandiseOrderDeliverySchedule> entity)
    {
        entity.ToTable("MerchandiseOrderDeliverySchedules", "Orders", table =>
            table.HasCheckConstraint("CK_MerchandiseOrderDeliverySchedules_Quantity", "\"Quantity\" > 0"));

        entity.HasKey(x => x.MerchandiseOrderDeliveryScheduleId);
        entity.Property(x => x.MerchandiseOrderDeliveryScheduleId)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(x => x.Quantity).HasPrecision(18, 3);
        entity.Property(x => x.RequestedDate).HasColumnType("date");
        entity.Property(x => x.PromisedDate).HasColumnType("date");
        entity.Property(x => x.RequestNote).HasColumnType("text");
        entity.Property(x => x.PromiseNote).HasColumnType("text");
        entity.Property(x => x.IsActive).HasDefaultValue(true);

        entity.HasIndex(x => new { x.MerchandiseOrderDetailId, x.IsActive, x.RequestedDate })
            .HasDatabaseName("IX_MO_DeliverySchedules_Detail_Active_RequestedDate");

        entity.HasOne(x => x.MerchandiseOrderDetail)
            .WithMany(x => x.DeliverySchedules)
            .HasForeignKey(x => x.MerchandiseOrderDetailId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_MerchandiseOrderDeliverySchedules_Detail");
    }
}
